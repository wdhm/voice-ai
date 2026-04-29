var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();
app.UseWebSockets();

var voiceLiveConfig = app.Configuration.GetSection("VoiceLive");
var vlEndpoint = Environment.GetEnvironmentVariable("AZURE_VOICELIVE_ENDPOINT")
    ?? voiceLiveConfig["Endpoint"]
    ?? "";
var vlModel = Environment.GetEnvironmentVariable("AZURE_VOICELIVE_MODEL")
    ?? voiceLiveConfig["Model"]
    ?? "gpt-realtime";
var vlVoice = Environment.GetEnvironmentVariable("AZURE_VOICELIVE_VOICE")
    ?? voiceLiveConfig["Voice"]
    ?? "en-US-Ava:DragonHDLatestNeural";

if (string.IsNullOrWhiteSpace(vlEndpoint))
    throw new InvalidOperationException(
        "Azure AI Foundry endpoint is required. Set the AZURE_VOICELIVE_ENDPOINT environment variable or VoiceLive:Endpoint in appsettings.json.");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/reset", () =>
{
    MockData.Reset();
    return Results.Ok(new { status = "reset", bookings = MockData.Bookings.Count });
});

app.MapGet("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var browserSocket = await context.WebSockets.AcceptWebSocketAsync();
    var handler = new VoiceLiveHandler(vlEndpoint, vlModel, vlVoice);
    await handler.HandleAsync(browserSocket, context.RequestAborted);
});

Console.WriteLine("Voice Bot backend starting on http://localhost:5000");
Console.WriteLine("Endpoint: {0}", vlEndpoint);
Console.WriteLine("WebSocket: ws://localhost:5000/ws");

app.Run("http://localhost:5000");
