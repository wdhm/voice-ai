using System.Text.Json;
using Azure.AI.VoiceLive;

/// <summary>
/// Validates booking references against the mock booking database.
/// </summary>
public sealed class AuthenticationTool
{
    public VoiceLiveFunctionDefinition Definition => new("authenticate_customer")
    {
        Description = "Authenticate a customer by validating their booking reference number. Must be called before accessing any personal booking data.",
        Parameters = BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                booking_reference = new
                {
                    type = "string",
                    description = "The customer's booking reference code, e.g. 'AA1234'"
                }
            },
            required = new[] { "booking_reference" }
        })
    };

    public object Execute(JsonElement args)
    {
        var reference = args.GetProperty("booking_reference").GetString()?.ToUpperInvariant() ?? "";
        var booking = MockData.Bookings.FirstOrDefault(b =>
            b.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));

        if (booking == null)
            return new { authenticated = false, message = "Booking reference not found. Please check the reference and try again." };

        return new { authenticated = true, booking_reference = booking.Reference, passenger_name = booking.PassengerName };
    }
}
