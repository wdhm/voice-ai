using System.Text.Json;
using Azure.AI.VoiceLive;

/// <summary>
/// Handles baggage-related operations: listing options and adding baggage to bookings.
/// </summary>
public sealed class BaggageTool
{
    public VoiceLiveFunctionDefinition GetOptionsDefinition => new("get_baggage_options")
    {
        Description = "Get available extra baggage options and pricing for a booking. Call this when a customer wants to add baggage.",
        Parameters = BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                booking_reference = new
                {
                    type = "string",
                    description = "The booking reference to check baggage options for"
                }
            },
            required = new[] { "booking_reference" }
        })
    };

    public VoiceLiveFunctionDefinition AddBaggageDefinition => new("add_baggage_to_booking")
    {
        Description = "Add extra baggage to a booking after the customer has confirmed. Returns a confirmation with updated booking details.",
        Parameters = BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                booking_reference = new
                {
                    type = "string",
                    description = "The booking reference to add baggage to"
                },
                baggage_type = new
                {
                    type = "string",
                    description = "The type of baggage to add, e.g. 'extra_checked_23kg', 'extra_checked_32kg', 'sports_equipment'"
                }
            },
            required = new[] { "booking_reference", "baggage_type" }
        })
    };

    public VoiceLiveFunctionDefinition RemoveBaggageDefinition => new("remove_baggage_from_booking")
    {
        Description = "Remove a previously added extra baggage item from a booking after the customer has confirmed. Only extra baggage that was added can be removed — included baggage cannot be removed.",
        Parameters = BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                booking_reference = new
                {
                    type = "string",
                    description = "The booking reference to remove baggage from"
                },
                baggage_type = new
                {
                    type = "string",
                    description = "The type of baggage to remove, e.g. 'extra_checked_23kg', 'extra_checked_32kg', 'sports_equipment'"
                }
            },
            required = new[] { "booking_reference", "baggage_type" }
        })
    };

    public object GetOptions(JsonElement args)
    {
        var reference = args.GetProperty("booking_reference").GetString()?.ToUpperInvariant() ?? "";
        var booking = MockData.Bookings.FirstOrDefault(b =>
            b.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));

        if (booking == null)
            return new { found = false, message = "Booking not found." };

        return new
        {
            found = true,
            booking_reference = booking.Reference,
            route = booking.Route,
            current_baggage = booking.IncludedBaggage,
            extra_baggage_already_added = booking.ExtraBaggage,
            available_options = MockData.BaggagePricing
        };
    }

    public object AddBaggage(JsonElement args)
    {
        var reference = args.GetProperty("booking_reference").GetString()?.ToUpperInvariant() ?? "";
        var baggageType = args.GetProperty("baggage_type").GetString() ?? "";

        var booking = MockData.Bookings.FirstOrDefault(b =>
            b.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));

        if (booking == null)
            return new { success = false, message = "Booking not found." };

        var pricing = MockData.BaggagePricing.FirstOrDefault(p =>
            p.Type.Equals(baggageType, StringComparison.OrdinalIgnoreCase));

        if (pricing == null)
            return new { success = false, message = $"Unknown baggage type: {baggageType}. Please choose from the available options." };

        // Simulate adding baggage
        booking.ExtraBaggage.Add(baggageType);
        MockData.Save();

        return new
        {
            success = true,
            message = $"Successfully added {pricing.Description} to booking {reference}.",
            price = pricing.Price,
            currency = pricing.Currency,
            updated_baggage = booking.ExtraBaggage,
            confirmation_number = $"BAG-{Random.Shared.Next(100000, 999999)}"
        };
    }

    public object RemoveBaggage(JsonElement args)
    {
        var reference = args.GetProperty("booking_reference").GetString()?.ToUpperInvariant() ?? "";
        var baggageType = args.GetProperty("baggage_type").GetString() ?? "";

        var booking = MockData.Bookings.FirstOrDefault(b =>
            b.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));

        if (booking == null)
            return new { success = false, message = "Booking not found." };

        var index = booking.ExtraBaggage.FindIndex(b =>
            b.Equals(baggageType, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
            return new { success = false, message = $"No extra baggage of type '{baggageType}' found on this booking." };

        booking.ExtraBaggage.RemoveAt(index);
        MockData.Save();

        return new
        {
            success = true,
            message = $"Successfully removed {baggageType} from booking {reference}.",
            updated_baggage = booking.ExtraBaggage
        };
    }
}
