# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Blazor Server application showcasing shipment tracking visualization using GeoBlazor Pro - a .NET wrapper for ArcGIS Maps SDK for JavaScript. The application displays shipments on an interactive map with various layers and widgets.

## Key Technologies

- **ASP.NET Core 9.0** - Web framework
- **Blazor Server** - Interactive server-side rendering
- **GeoBlazor Pro 4.1.0** - GIS mapping components for Blazor
- **Radzen Blazor 4.23.4** - UI component library
- **ArcGIS Maps SDK** - Underlying mapping technology

## Development Commands

```bash
# Build the application
dotnet build

# Run the application (development mode)
dotnet run

# Run with specific profile
dotnet run --launch-profile https

# Restore packages
dotnet restore
```

The application runs on:
- HTTPS: https://localhost:7139
- HTTP: http://localhost:5141

## Architecture Overview

### Core Components

- **Program.cs** - Application startup, configures GeoBlazor Pro and dependency injection
- **Repository.cs** - Data access layer that loads shipment data from `export.json`
- **Records.cs** - Domain models including `Shipment`, `ShipmentStatus`, `SearchCriteria`, and `CategoryColors`

### Data Flow

1. **Data Source**: Shipment data is loaded from `export.json` (sample data from cobbl.io)
2. **Repository Pattern**: `Repository` class provides data access methods and generates synthetic tracking data
3. **Map Visualization**: `MapPage.razor` converts shipments to graphics and displays them on the map

### GeoBlazor Integration

The application heavily uses GeoBlazor Pro components:
- **MapView** - Main map container with basemap and layers
- **GraphicsLayer** - Displays shipment locations as graphics
- **FeatureLayer** - Weather data overlay
- **MapImageLayer** - Traffic data overlay
- **Widgets** - Layer list, legend, time slider, and custom controls

### Key Patterns

- **Dependency Injection**: Repository is registered as singleton in Program.cs
- **Component Lifecycle**: Graphics are added to map after view renders to avoid timing issues
- **Symbol Styling**: Category-based color coding using `CategoryColors` dictionary
- **Popup Templates**: Rich popups with field information for each shipment

## Important Files

- **Components/Pages/MapPage.razor** - Main map implementation with shipment visualization
- **Repository.cs** - Data access and synthetic data generation
- **Records.cs** - Domain models and color mapping
- **export.json** - Sample shipment data (first 100 records are used)
- **Components/_Imports.razor** - Global using statements for GeoBlazor namespaces

## Configuration Notes

- **NuGet Config**: Uses local packages from `../packages` directory
- **Launch Settings**: Configured for both HTTP and HTTPS development profiles
- **User Secrets**: Enabled for secure configuration storage
- **GeoBlazor Pro**: Requires proper licensing configuration in appsettings or user secrets

## Data Structure

Shipments include both location data (lat/lng) and synthetic tracking information:
- Basic product data from JSON file
- Generated tracking numbers, customer info, and delivery status
- Time-based data for temporal analysis
- Category-based color coding for visual grouping