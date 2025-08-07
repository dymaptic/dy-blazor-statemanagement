# ShipmentTracker Enhancement Plan

## 1. Fix Non-Functional Buttons

### NavMenu Links
- Update `NavMenu.razor` to replace `href="#"` with proper routes
- Create pages for Inventory, Reports, and Settings
- Implement basic functionality for each page

## 2. Implement Shipment Search Feature

### Create SearchShipment.razor Page
```csharp
@page "/search"
// Search form with fields:
// - Tracking Number
// - Customer Name
// - Date Range
// - Status Filter
```

### Update Repository.cs
```csharp
public Shipment? GetShipmentByTrackingNumber(string trackingNumber)
public List<Shipment> SearchShipments(SearchCriteria criteria)
```

### Add to Records.cs
```csharp
public record SearchCriteria(
    string? TrackingNumber,
    string? CustomerName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    ShipmentStatus? Status);

public enum ShipmentStatus { InTransit, Delivered, Pending, Delayed }
```

## 3. Create Individual Shipment Tracking Page

### Create TrackShipment.razor
```csharp
@page "/track/{TrackingNumber}"
// Display:
// - Real-time location on map
// - Detailed timeline with all stops
// - Estimated delivery time
// - Package details
// - Delivery instructions form
```

### Enhance Shipment Record
```csharp
public record Shipment(
    // ... existing fields ...
    string TrackingNumber,
    string CustomerName,
    string CustomerEmail,
    string OriginAddress,
    string DestinationAddress,
    ShipmentStatus Status,
    DateTime CreatedDate,
    DateTime? DeliveredDate,
    List<TrackingEvent> Events);

public record TrackingEvent(
    DateTime Timestamp,
    string Location,
    string Description,
    double? Latitude,
    double? Longitude);
```

## 4. Implement Pickup Request Form

### Create PickupRequest.razor
```csharp
@page "/pickup"
// Form fields:
// - Sender Information (Name, Email, Phone, Address)
// - Package Details (Weight, Dimensions, Category)
// - Pickup Date/Time Window
// - Special Instructions
```

### Add PickupRequest Record
```csharp
public record PickupRequest(
    int Id,
    string SenderName,
    string SenderEmail,
    string SenderPhone,
    string PickupAddress,
    DateTime RequestedPickupDate,
    string TimeWindow,
    PackageDetails Package,
    string? SpecialInstructions,
    PickupStatus Status);

public enum PickupStatus { Pending, Scheduled, Completed, Cancelled }
```

## 5. Add User Authentication

### Implement Basic Authentication
- Create Login.razor page
- Add UserService for authentication
- Implement role-based access (Customer, Employee, Admin)
- Secure sensitive pages with authorization

### User Records
```csharp
public record User(
    int Id,
    string Email,
    string Name,
    UserRole Role,
    List<int> ShipmentIds);

public enum UserRole { Customer, Employee, Admin }
```

## 6. Enhance Map Features

### MapPage.razor Improvements
- Fix weather/traffic toggle buttons
- Add route optimization for multiple deliveries
- Implement delivery zones visualization
- Add driver tracking simulation

## 7. Add Notification System

### Create NotificationService
- Email notifications for status updates
- SMS alerts for delivery windows
- In-app notifications for logged-in users

## 8. Implement Reports Page

### Create Reports.razor
- Delivery performance metrics
- Package volume by category
- Revenue analytics
- Customer satisfaction ratings

## 9. Database Integration

### Replace JSON with Database
- Add Entity Framework Core
- Create ShipmentContext
- Implement migrations
- Update Repository to use database

## 10. API Development

### Create ShipmentController
```csharp
[ApiController]
[Route("api/[controller]")]
public class ShipmentController
{
    [HttpGet("track/{trackingNumber}")]
    [HttpPost("pickup")]
    [HttpGet("search")]
}
```

## Implementation Priority

1. Fix non-functional buttons (Quick win)
2. Create search functionality
3. Implement individual tracking page
4. Add pickup request form
5. Fix map toggle buttons
6. Add basic authentication
7. Enhance with notifications
8. Add reporting features
9. Integrate database
10. Develop API endpoints