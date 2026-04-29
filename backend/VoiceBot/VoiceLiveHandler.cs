using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Azure.AI.VoiceLive;
using Azure.Identity;

/// <summary>
/// Bridges a browser WebSocket to the Voice Live API.
/// Browser sends raw PCM16 audio frames → forwarded to Voice Live.
/// Voice Live sends audio/events back → forwarded to browser as binary + JSON.
/// Function calls are intercepted and executed server-side.
/// </summary>
public sealed class VoiceLiveHandler
{
    private readonly string _endpoint;
    private readonly string _model;
    private readonly string _voiceName;
    private readonly ToolRegistry _tools = new();

    public VoiceLiveHandler(string endpoint, string model, string voiceName)
    {
        _endpoint = endpoint;
        _model = model;
        _voiceName = voiceName;
    }

    public async Task HandleAsync(WebSocket browserSocket, CancellationToken ct)
    {
        Console.WriteLine("[VoiceLive] New session starting...");
        var credential = new DefaultAzureCredential();
        var client = new VoiceLiveClient(new Uri(_endpoint), credential);

        await using var session = await client.StartSessionAsync(_model, ct);
        Console.WriteLine("[VoiceLive] Session started, configuring...");

        await ConfigureSessionAsync(session, ct);

        // Trigger initial greeting
        var greeting = new SystemMessageItem(
            new InputTextContentPart("Greet the user with your opening line."));
        await session.AddItemAsync(greeting, ct);
        await session.StartResponseAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var receiveFromBrowser = ReceiveFromBrowserAsync(browserSocket, session, cts.Token);
        var receiveFromVoiceLive = ReceiveFromVoiceLiveAsync(session, browserSocket, cts.Token);

        // When either side disconnects, cancel the other
        var completed = await Task.WhenAny(receiveFromBrowser, receiveFromVoiceLive);
        await cts.CancelAsync();

        try { await Task.WhenAll(receiveFromBrowser, receiveFromVoiceLive); }
        catch (OperationCanceledException) { }

        Console.WriteLine("[VoiceLive] Session ended.");
    }

    private async Task ConfigureSessionAsync(VoiceLiveSession session, CancellationToken ct)
    {
        var turnDetection = new AzureSemanticVadTurnDetectionMultilingual
        {
            Threshold = 0.5f,
            PrefixPadding = TimeSpan.FromMilliseconds(300),
            SilenceDuration = TimeSpan.FromMilliseconds(500),
            RemoveFillerWords = true,
            AutoTruncate = true,
        };
        turnDetection.Languages.Add("en");

        var options = new VoiceLiveSessionOptions
        {
            Instructions = SystemPrompt.Text,
            Voice = new AzureStandardVoice(_voiceName),
            InputAudioFormat = InputAudioFormat.Pcm16,
            OutputAudioFormat = OutputAudioFormat.Pcm16,
            TurnDetection = turnDetection,
            InputAudioEchoCancellation = new AudioEchoCancellation(),
            InputAudioNoiseReduction = new AudioNoiseReduction(AudioNoiseReductionType.AzureDeepNoiseSuppression),
            ToolChoice = ToolChoiceLiteral.Auto,
        };

        options.Modalities.Clear();
        options.Modalities.Add(InteractionModality.Text);
        options.Modalities.Add(InteractionModality.Audio);

        foreach (var tool in _tools.GetDefinitions())
            options.Tools.Add(tool);

        await session.ConfigureSessionAsync(options, ct);
        Console.WriteLine("[VoiceLive] Session configured with {0} tools.", options.Tools.Count);
    }

    private static async Task ReceiveFromBrowserAsync(WebSocket browserSocket, VoiceLiveSession session, CancellationToken ct)
    {
        var buffer = new byte[8192];
        try
        {
            while (browserSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await browserSocket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                {
                    var audio = new byte[result.Count];
                    Buffer.BlockCopy(buffer, 0, audio, 0, result.Count);
                    await session.SendInputAudioAsync(audio, ct);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private async Task ReceiveFromVoiceLiveAsync(VoiceLiveSession session, WebSocket browserSocket, CancellationToken ct)
    {
        try
        {
            await foreach (var update in session.GetUpdatesAsync(ct))
            {
                switch (update)
                {
                    case SessionUpdateSessionUpdated:
                        Console.WriteLine("[VoiceLive] Session updated / ready.");
                        await SendJsonAsync(browserSocket, new { type = "session.ready" }, ct);
                        break;

                    case SessionUpdateInputAudioBufferSpeechStarted:
                        await SendJsonAsync(browserSocket, new { type = "speech.started" }, ct);
                        break;

                    case SessionUpdateInputAudioBufferSpeechStopped:
                        await SendJsonAsync(browserSocket, new { type = "speech.stopped" }, ct);
                        break;

                    case SessionUpdateResponseAudioDelta audioDelta:
                        if (audioDelta.Delta is { } audioData)
                        {
                            var bytes = audioData.ToArray();
                            if (bytes.Length > 0 && browserSocket.State == WebSocketState.Open)
                                await browserSocket.SendAsync(bytes, WebSocketMessageType.Binary, true, ct);
                        }
                        break;

                    case SessionUpdateResponseAudioTranscriptDelta transcriptDelta:
                        await SendJsonAsync(browserSocket, new
                        {
                            type = "transcript.assistant",
                            text = transcriptDelta.Delta
                        }, ct);
                        break;

                    case SessionUpdateConversationItemInputAudioTranscriptionCompleted userTranscript:
                        await SendJsonAsync(browserSocket, new
                        {
                            type = "transcript.user",
                            text = userTranscript.Transcript
                        }, ct);
                        break;

                    // Handle function calls immediately for lowest latency
                    case SessionUpdateResponseFunctionCallArgumentsDone functionCall:
                        Console.WriteLine("[VoiceLive] Function call: {0}", functionCall.Name);
                        await SendJsonAsync(browserSocket, new
                        {
                            type = "function.calling",
                            name = functionCall.Name,
                            arguments = functionCall.Arguments
                        }, ct);

                        var result = _tools.Execute(functionCall.Name, functionCall.Arguments);
                        var resultJson = JsonSerializer.Serialize(result);

                        // Send function result summary to the browser for demo visualization
                        await SendJsonAsync(browserSocket, new
                        {
                            type = "function.result",
                            name = functionCall.Name,
                            result = result
                        }, ct);

                        // Push booking/baggage data to the browser UI on relevant tool calls
                        await SendBookingUpdateIfRelevant(
                            browserSocket, functionCall.Name, functionCall.Arguments, ct);

                        if (functionCall.Name == "get_baggage_options")
                        {
                            await SendJsonAsync(browserSocket, new
                            {
                                type = "baggage.options",
                                options = MockData.BaggagePricing.Select(o => new
                                {
                                    type = o.Type,
                                    description = o.Description,
                                    price = o.Price,
                                    currency = o.Currency
                                })
                            }, ct);
                        }

                        await session.AddItemAsync(
                            new FunctionCallOutputItem(functionCall.CallId, resultJson), ct);
                        await session.StartResponseAsync(ct);
                        Console.WriteLine("[VoiceLive] Function result sent for: {0}", functionCall.Name);
                        break;

                    case SessionUpdateError errorUpdate:
                        Console.WriteLine("[VoiceLive] Error: {0}", errorUpdate.Error.Message);
                        await SendJsonAsync(browserSocket, new
                        {
                            type = "error",
                            message = errorUpdate.Error.Message
                        }, ct);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine("[VoiceLive] Unexpected error: {0}", ex.Message);
        }
    }

    private static async Task SendJsonAsync(WebSocket socket, object message, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    /// <summary>
    /// After authenticate, get_booking_details, or add_baggage_to_booking,
    /// push the current booking state to the browser so the UI can show/update it.
    /// </summary>
    private static async Task SendBookingUpdateIfRelevant(
        WebSocket browserSocket, string functionName, string arguments, CancellationToken ct)
    {
        string[] bookingTools = ["authenticate_customer", "get_booking_details", "add_baggage_to_booking", "remove_baggage_from_booking", "send_confirmation_email"];
        if (!bookingTools.Contains(functionName)) return;

        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);
            var reference = args.GetProperty("booking_reference").GetString()?.ToUpperInvariant() ?? "";
            var booking = MockData.Bookings.FirstOrDefault(b =>
                b.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));

            if (booking == null) return;

            // For authenticate, only send if we know the booking exists (auth succeeded)
            var eventType = functionName == "add_baggage_to_booking" ? "booking.updated" : "booking.loaded";

            await SendJsonAsync(browserSocket, new
            {
                type = eventType,
                booking = new
                {
                    reference = booking.Reference,
                    passengerName = booking.PassengerName,
                    email = booking.Email,
                    route = booking.Route,
                    departureDate = booking.DepartureDate,
                    flightNumber = booking.FlightNumber,
                    ticketClass = booking.TicketClass,
                    includedBaggage = booking.IncludedBaggage,
                    extraBaggage = booking.ExtraBaggage,
                    confirmationEmailSent = booking.ConfirmationEmailSent,
                    status = booking.Status
                }
            }, ct);
        }
        catch { /* Don't break the flow if UI push fails */ }
    }
}
