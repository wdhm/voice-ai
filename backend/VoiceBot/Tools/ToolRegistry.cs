using System.Text.Json;
using Azure.AI.VoiceLive;

/// <summary>
/// Registry that holds all function tool definitions and their implementations.
/// Tools are resolved by name and executed with JSON arguments.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, Func<JsonElement, object>> _handlers = new();
    private readonly List<VoiceLiveFunctionDefinition> _definitions = [];

    public ToolRegistry()
    {
        var kb = new KnowledgeBaseTool();
        var auth = new AuthenticationTool();
        var booking = new BookingTool();
        var baggage = new BaggageTool();
        var escalation = new EscalationTool();
        var confirmationEmail = new ConfirmationEmailTool();

        Register(kb.Definition, kb.Execute);
        Register(auth.Definition, auth.Execute);
        Register(booking.Definition, booking.Execute);
        Register(baggage.GetOptionsDefinition, baggage.GetOptions);
        Register(baggage.AddBaggageDefinition, baggage.AddBaggage);
        Register(baggage.RemoveBaggageDefinition, baggage.RemoveBaggage);
        Register(escalation.Definition, escalation.Execute);
        Register(confirmationEmail.Definition, confirmationEmail.Execute);
    }

    private void Register(VoiceLiveFunctionDefinition definition, Func<JsonElement, object> handler)
    {
        _definitions.Add(definition);
        _handlers[definition.Name] = handler;
    }

    public IEnumerable<VoiceLiveFunctionDefinition> GetDefinitions() => _definitions;

    public object Execute(string name, string argumentsJson)
    {
        if (!_handlers.TryGetValue(name, out var handler))
            return new { error = $"Unknown tool: {name}" };

        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
            return handler(args);
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }
}
