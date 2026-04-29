using System.Text.Json;
using Azure.AI.VoiceLive;

/// <summary>
/// Retrieves booking details from the mock database.
/// </summary>
public sealed class BookingTool
{
    public VoiceLiveFunctionDefinition Definition => new("get_booking_details")
    {
        Description = "Retrieve full booking details for an authenticated customer including flights, passengers, baggage, and confirmation status.",
        Parameters = BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                booking_reference = new
                {
                    type = "string",
                    description = "The authenticated booking reference code"
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
            return new { found = false, message = "Booking not found." };

        return new
        {
            found = true,
            reference = booking.Reference,
            passenger_name = booking.PassengerName,
            route = booking.Route,
            departure_date = booking.DepartureDate,
            flight_number = booking.FlightNumber,
            ticket_class = booking.TicketClass,
            included_baggage = booking.IncludedBaggage,
            extra_baggage = booking.ExtraBaggage,
            confirmation_email_sent = booking.ConfirmationEmailSent,
            confirmation_email = booking.Email,
            status = booking.Status
        };
    }
}
