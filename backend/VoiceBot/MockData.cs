using System.Text.Json;

/// <summary>
/// In-memory mock data for demo scenarios.
/// Persists booking changes to a JSON file so they survive restarts.
/// </summary>
public static class MockData
{
    private static readonly string PersistPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "bookings-state.json");

    public static readonly List<Booking> Bookings = LoadOrDefault();

    public static readonly List<BaggageOption> BaggagePricing =
    [
        new BaggageOption("extra_checked_23kg", "Extra checked bag (max 23 kg)", 45, "EUR"),
        new BaggageOption("extra_checked_32kg", "Extra checked bag (max 32 kg)", 65, "EUR"),
        new BaggageOption("overweight_23_to_32", "Overweight upgrade 23 kg → 32 kg", 35, "EUR"),
        new BaggageOption("sports_equipment", "Sports equipment (ski, golf, bike, etc.)", 55, "EUR"),
        new BaggageOption("musical_instrument", "Musical instrument as checked bag", 55, "EUR"),
    ];

    /// <summary>Save current booking state to disk.</summary>
    public static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Bookings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PersistPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[MockData] Failed to save: {0}", ex.Message);
        }
    }

    /// <summary>Reset bookings to factory defaults (deletes persisted file).</summary>
    public static void Reset()
    {
        Bookings.Clear();
        Bookings.AddRange(DefaultBookings());
        if (File.Exists(PersistPath)) File.Delete(PersistPath);
        Console.WriteLine("[MockData] Reset to defaults.");
    }

    private static List<Booking> LoadOrDefault()
    {
        try
        {
            if (File.Exists(PersistPath))
            {
                var json = File.ReadAllText(PersistPath);
                var loaded = JsonSerializer.Deserialize<List<Booking>>(json);
                if (loaded is { Count: > 0 })
                {
                    Console.WriteLine("[MockData] Loaded {0} booking(s) from disk.", loaded.Count);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[MockData] Failed to load persisted state: {0}", ex.Message);
        }

        return DefaultBookings();
    }

    private static List<Booking> DefaultBookings() =>
    [
        new Booking
        {
            Reference = "AA1234",
            PassengerName = "Erik Johansson",
            Email = "erik.johansson@email.com",
            Route = "CPH → OSL",
            DepartureDate = "2026-04-15",
            FlightNumber = "AT1472",
            TicketClass = "Atlas Go",
            IncludedBaggage = "1 cabin bag (max 8 kg)",
            ExtraBaggage = [],
            ConfirmationEmailSent = true,
            Status = "Confirmed"
        },
        new Booking
        {
            Reference = "AA5678",
            PassengerName = "Anna Lindström",
            Email = "anna.lindstrom@email.com",
            Route = "ARN → LHR",
            DepartureDate = "2026-04-22",
            FlightNumber = "AT1523",
            TicketClass = "Atlas Plus",
            IncludedBaggage = "1 cabin bag (max 8 kg) + 1 checked bag (max 23 kg)",
            ExtraBaggage = [],
            ConfirmationEmailSent = false,
            Status = "Confirmed"
        },
        new Booking
        {
            Reference = "AA9012",
            PassengerName = "Magnus Berg",
            Email = "magnus.berg@email.com",
            Route = "CPH → JFK",
            DepartureDate = "2026-05-10",
            FlightNumber = "AT0901",
            TicketClass = "Atlas Business",
            IncludedBaggage = "1 cabin bag (max 8 kg) + 2 checked bags (max 32 kg each)",
            ExtraBaggage = [],
            ConfirmationEmailSent = true,
            Status = "Confirmed"
        }
    ];
}

public class Booking
{
    public required string Reference { get; set; }
    public required string PassengerName { get; set; }
    public required string Email { get; set; }
    public required string Route { get; set; }
    public required string DepartureDate { get; set; }
    public required string FlightNumber { get; set; }
    public required string TicketClass { get; set; }
    public required string IncludedBaggage { get; set; }
    public required List<string> ExtraBaggage { get; set; }
    public required bool ConfirmationEmailSent { get; set; }
    public required string Status { get; set; }
}

public record BaggageOption(string Type, string Description, decimal Price, string Currency);
