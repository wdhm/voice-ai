using System.Text.Json;
using Azure.AI.VoiceLive;

/// <summary>
/// Simulates sending a booking confirmation email and updates the booking state.
/// </summary>
public sealed class ConfirmationEmailTool
{
    public VoiceLiveFunctionDefinition Definition => new("send_confirmation_email")
    {
        Description = "Send (or resend) a booking confirmation email to the customer. Use when the customer says they haven't received their confirmation, or asks to have it resent. Updates the booking to mark the email as sent.",
        Parameters = BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                booking_reference = new
                {
                    type = "string",
                    description = "The booking reference to send the confirmation email for"
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
            return new { success = false, message = "Booking not found." };

        booking.ConfirmationEmailSent = true;
        MockData.Save();

        Console.WriteLine($"[EMAIL] Confirmation sent to {booking.Email} for {booking.Reference}");

        return new
        {
            success = true,
            message = $"Confirmation email sent to {booking.Email}",
            booking_reference = booking.Reference,
            passenger_name = booking.PassengerName,
            email = booking.Email
        };
    }
}
