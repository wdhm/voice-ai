using System.Text.Json;
using Azure.AI.VoiceLive;

/// <summary>
/// Handles escalation to a human agent when the bot cannot resolve an issue.
/// </summary>
public sealed class EscalationTool
{
    public VoiceLiveFunctionDefinition Definition => new("escalate_to_agent")
    {
        Description = "Escalate the conversation to a human agent. Use when the customer's issue is too complex, you are not confident in your answer, or the customer explicitly requests a human agent.",
        Parameters = BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                reason = new
                {
                    type = "string",
                    description = "Brief description of why the escalation is needed"
                },
                booking_reference = new
                {
                    type = "string",
                    description = "The customer's booking reference if available"
                },
                conversation_summary = new
                {
                    type = "string",
                    description = "A brief summary of the conversation so far, including what the customer asked about, what was discussed, and what remains unresolved. This context will be passed to the human agent."
                }
            },
            required = new[] { "reason", "conversation_summary" }
        })
    };

    public object Execute(JsonElement args)
    {
        var reason = args.GetProperty("reason").GetString() ?? "Unspecified";
        var bookingRef = args.TryGetProperty("booking_reference", out var br) ? br.GetString() : null;
        var conversationSummary = args.GetProperty("conversation_summary").GetString() ?? "";

        Console.WriteLine($"[ESCALATION] Reason: {reason}, Booking: {bookingRef ?? "N/A"}, Summary: {conversationSummary}");

        return new
        {
            escalated = true,
            message = "I'm transferring you to a human agent now. They'll have the context of our conversation. Please hold for a moment.",
            estimated_wait = "2-3 minutes",
            reference_number = $"ESC-{Random.Shared.Next(100000, 999999)}",
            conversation_summary = conversationSummary
        };
    }
}
