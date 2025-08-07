using System.Text.Json;
using ShipmentTracker.Client;

namespace ShipmentTracker;

public static class Repository
{
    public static List<Shipment> GetShipments()
    {
        string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "export.json");
        string json = File.ReadAllText(jsonPath);
        var basicShipments = JsonSerializer.Deserialize<List<BasicShipment>>(json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            })!;

        // Enhance shipments with additional data
        return basicShipments.Take(100).Select(s => new Shipment
        {
            ItemId = s.Id,
            Category = s.Category,
            SubCategory = s.SubCategory,
            Name = s.Name,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            Quantity = s.Quantity,
            Weight = s.Weight,
            TrackingNumber = GenerateTrackingNumber(s.Id),
            CustomerName = GenerateCustomerName(),
            CustomerEmail = GenerateCustomerEmail(),
            OriginAddress = "1234 Warehouse Dr, Chicago, IL 60601",
            DestinationAddress = GenerateDestinationAddress(),
            Status = GenerateStatus(),
            CreatedDate = DateTime.Now.AddDays(-Random.Next(1, 30)),
            DeliveredDate = GenerateDeliveredDate()
        }).ToList();
    }

    private static string GenerateTrackingNumber(int id) => $"LT{DateTime.Now.Year}{id:D6}";
    
    private static string GenerateCustomerName()
    {
        var firstNames = new[] { "John", "Jane", "Michael", "Sarah", "David", "Emma", "Robert", "Lisa" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis" };
        return $"{firstNames[Random.Next(firstNames.Length)]} {lastNames[Random.Next(lastNames.Length)]}";
    }

    private static string GenerateCustomerEmail()
    {
        var domains = new[] { "gmail.com", "yahoo.com", "outlook.com", "company.com" };
        return $"customer{Random.Next(1000, 9999)}@{domains[Random.Next(domains.Length)]}";
    }

    private static string GenerateDestinationAddress()
    {
        var streets = new[] { "Main St", "Oak Ave", "Elm Dr", "Park Blvd", "First St" };
        var cities = new[] { "New York, NY", "Los Angeles, CA", "Houston, TX", "Phoenix, AZ", "Philadelphia, PA" };
        return $"{Random.Next(100, 9999)} {streets[Random.Next(streets.Length)]}, {cities[Random.Next(cities.Length)]} {Random.Next(10000, 99999)}";
    }

    private static ShipmentStatus GenerateStatus()
    {
        var rand = Random.Next(100);
        if (rand < 60) return ShipmentStatus.InTransit;
        if (rand < 80) return ShipmentStatus.Delivered;
        if (rand < 95) return ShipmentStatus.Pending;
        return ShipmentStatus.Delayed;
    }

    private static DateTime? GenerateDeliveredDate()
    {
        return Random.Next(100) < 20 ? DateTime.Now.AddDays(-Random.Next(1, 10)) : null;
    }

    private record BasicShipment(
        int Id,
        string Category,
        string SubCategory,
        string Name,
        double Latitude,
        double Longitude,
        int Quantity,
        double Weight
    );

    private static readonly Random Random = new();
}