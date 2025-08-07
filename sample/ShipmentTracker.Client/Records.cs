using System.ComponentModel.DataAnnotations;
using dymaptic.Blazor.StateManagement;
using dymaptic.GeoBlazor.Core.Model;

namespace ShipmentTracker.Client;

public record Shipment : StateRecord
{
    public int ItemId { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Quantity { get; set; }
    public double Weight { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? OriginAddress { get; set; }
    public string? DestinationAddress { get; set; }
    public ShipmentStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
}

public record PickupRequestModel: StateRecord
{
    [Required(ErrorMessage = "Name is required")]
    public string SenderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string SenderEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Invalid phone number")]
    public string SenderPhone { get; set; } = string.Empty;

    public string? Company { get; set; }

    [Required(ErrorMessage = "Street address is required")]
    public string PickupAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "State is required")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "ZIP code is required")]
    public string ZipCode { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Number of packages must be between 1 and 100")]
    public int NumberOfPackages { get; set; } = 1;

    [Range(0.1, 1000, ErrorMessage = "Weight must be between 0.1 and 1000 lbs")]
    public decimal TotalWeight { get; set; } = 1;

    [Required(ErrorMessage = "Category is required")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Package size is required")]
    public string PackageSize { get; set; } = string.Empty;

    [Required(ErrorMessage = "Pickup date is required")]
    public DateTime? RequestedPickupDate { get; set; }

    [Required(ErrorMessage = "Time window is required")]
    public string TimeWindow { get; set; } = string.Empty;

    public string? SpecialInstructions { get; set; }
}

public record MonthlyStat: StateRecord
{
    public string Month { get; init; } = string.Empty;
    public int TotalShipments { get; init; }
    public decimal Revenue { get; init; }
    public int OnTimeRate { get; init; }
}

public record CompanySettings : StateRecord
{
    public string CompanyName { get; set; } = string.Empty;
    public string? WarehouseLocation { get; set; }
    public bool EnableNotifications { get; set; }
    public bool ShowTrafficByDefault { get; set; }
    public bool ShowWeatherByDefault { get; set; }
    public int DefaultZoom { get; set; }
    public string? ArcgisApiKey { get; set; }
    public string? EmailApiKey { get; set; }
}

public enum ShipmentStatus { Pending, InTransit, Delivered, Delayed }

public record TrackingEvent(
    DateTime Timestamp,
    string Location,
    string Description,
    double? Latitude = null,
    double? Longitude = null);

public static class CategoryColors
{
    public static readonly Dictionary<string, MapColor> AllColors = new()
    {
        {"Other", Other},
        {"Beauty", Beauty},
        {"Electronics", Electronics},
        {"Entertainment", Entertainment},
        {"Home", Home},
        {"Activity", Activity},
        {"Health", Health},
        {"Pets", Pets},
        {"Clothing", Clothing},
        {"Automotive", Automotive}
    };
    
    public static MapColor Other => new("blue");
    public static MapColor Beauty => new("yellow");
    public static MapColor Electronics => new("green");
    public static MapColor Entertainment => new("red");
    public static MapColor Home => new("orange");
    public static MapColor Activity => new("purple");
    public static MapColor Health => new("brown");
    public static MapColor Pets => new("pink");
    public static MapColor Clothing => new("black");
    public static MapColor Automotive => new("lightgreen");
}