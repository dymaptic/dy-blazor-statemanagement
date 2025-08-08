# Blazor State Management Library

A comprehensive state management library for Blazor Web Apps that seamlessly works across Client and Server render modes, providing unified state management with caching, persistence, and undo/redo functionality.

## Overview

This library provides one solution for managing application state in Blazor applications that use both 
Interactive Server Mode (server-side rendering with SignalR websockets) and Interactive WebAssembly Mode (client-side rendering). 
It automatically handles state synchronization, caching, and persistence across different rendering modes.

## Features

### Core Functionality
- **MVSM Pattern**: Implements the Model-View-State-Manager pattern
  - _Model_: C# record types representing state, defined in this library as inheriting from `StateRecord`
  - _View_: Blazor components inheriting from `StateComponentBase<T>` or `CollectionStateComponentBase<T>`
  - _State Manager_: `IStateManager<T>` interface with implementations for client (`ClientStateManager<T>`) and server (`ServerStateManager<T>`)
- **Generic State Management**: Works with any C# record type that inherits from `StateRecord`
- **CRUD Operations**: Create, Read, Update, Delete operations for state records
- **Querying and Searching**: Flexible querying with URL parameter integration
- **Bulk Operations**: Save, update, delete multiple records in a single operation
- **Cross-Render Mode Support**: Components built on `StateComponentBase` and `CollectionStateComponentBase` work with 
  both Server and Client render modes, as well as Interactive Auto mode.
- **State Persistence**: Automatic state saving and loading with configurable persistence strategies
- **Caching**: Multi-level caching with IndexedDB (client) and HybridCache (server)
- **Undo/Redo**: Built-in undo/redo functionality for state changes
- **Authentication Integration**: Integrated with ASP.NET Core Identity and authentication
- **Real-time Updates**: State synchronization across components and sessions

### Storage Options
- **Client-Side**: IndexedDB for offline-capable state persistence
- **Server-Side**: Entity Framework Core with SQL database backend
- **Hybrid Caching**: Microsoft.Extensions.Caching.Hybrid for optimized performance
- **Session Storage**: Browser session storage for temporary state

### Developer Experience
- **Type-Safe**: Strongly-typed state management with C# records
- **Component Base Classes**: Ready-to-use base components for rapid development
- **Dependency Injection**: Full integration with .NET DI container
- **Logging**: Comprehensive logging throughout the state management pipeline

## Architecture

The library consists of two main packages:

### dymaptic.Blazor.StateManagement (.NET 9.0)
Core client-side library that provides:
- `StateComponentBase<T>`: Base component class for state-aware components
- `CollectionStateComponentBase<T>`: Base component for managing collections
- `ClientStateManager<T>`: Client-side state management implementation
- `IndexedDb`: Browser IndexedDB integration
- `SessionStorage`: Browser session storage wrapper

### dymaptic.Blazor.StateManagement.Server (.NET 9.0)
Server-side extensions that provide:
- `ServerStateManager<T>`: Server-side state management implementation
- `StateManagementDbContext`: Entity Framework Core context
- `StateManagementApi`: RESTful API endpoints for state operations
- `ApplicationUser`: Identity integration

## Quick Start

### 1. Install Packages

```bash
# For client-side projects
dotnet add package dymaptic.Blazor.StateManagement

# For server-side projects
dotnet add package dymaptic.Blazor.StateManagement.Server
```

### 2. Define Your State Model

```csharp
public record MyStateRecord : StateRecord
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // Add your custom properties here
}
```

### 3. Create State-Aware Components

```csharp
@inherits StateComponentBase<MyStateRecord>

<h3>My Component</h3>

@if (Model != null)
{
    <input @bind="Model.Name" />
    <textarea @bind="Model.Description"></textarea>
    
    <button @onclick="() => Update()">Save</button>
    <button @onclick="() => Undo()">Undo</button>
    <button @onclick="() => Redo()">Redo</button>
}

@code {
    protected override bool LoadOnInitialize => true;
    
    protected override async Task OnInitializedAsync()
    {
        if (LoadOnInitialize)
        {
            await Load();
        }
    }
}
```

### 4. Configure Services

**Client-side (Program.cs):**
```csharp
builder.Services.AddStateManagement<MyStateRecord>();
```

**Server-side (Program.cs):**
```csharp
builder.Services.AddServerStateManagement<MyStateRecord>(builder.Configuration);
```

## Sample Application

The repository includes a comprehensive sample application - **ShipmentTracker** - that demonstrates:

- Real-world usage patterns
- GeoBlazor integration for mapping functionality
- Client-server state synchronization
- Authentication and authorization
- Complex state management scenarios

The sample application tracks shipments with geospatial data visualization, showcasing how the state management library handles complex data structures and real-time updates.

## Project Structure

```
├── src/
│   ├── dymaptic.Blazor.StateManagement/          # Core client library
│   │   ├── Interfaces/                           # Core interfaces
│   │   ├── StateComponentBase.cs                 # Base component classes
│   │   ├── ClientStateManager.cs                 # Client-side state manager
│   │   ├── IndexedDb.cs                         # Browser storage
│   │   └── StateRecord.cs                       # Base state record
│   └── dymaptic.Blazor.StateManagement.Server/  # Server extensions
│       ├── ServerStateManager.cs                # Server-side state manager
│       ├── StateManagementDbContext.cs          # EF Core context
│       └── StateManagementApi.cs                # API endpoints
└── sample/
    ├── ShipmentTracker/                         # Server-side demo app
    └── ShipmentTracker.Client/                  # Client-side demo app
```

## Key Concepts

### State Records
All state objects inherit from `StateRecord`, providing:
- Unique identification (`Id`)
- Timestamp tracking (`CreatedUtc`, `LastUpdatedUtc`, `LastSavedUtc`)
- Creator tracking (`CreatorId`)
- Equality comparison based on ID

### State Managers
- **IStateManager<T>**: Core interface defining CRUD operations
- **ClientStateManager<T>**: Handles client-side state with IndexedDB persistence
- **ServerStateManager<T>**: Manages server-side state with database persistence

### Component Base Classes
- **StateComponentBase<T>**: Single record management
- **CollectionStateComponentBase<T>**: Collection management with bulk operations

## Advanced Features

### Undo/Redo System
Built-in undo/redo functionality tracks state changes automatically:
```csharp
await Undo();  // Revert to previous state
await Redo();  // Reapply undone changes
```

### Query and Search
Flexible querying with URL parameter integration:
```csharp
await Search(new Dictionary<string, string> { ["name"] = "searchTerm" });
```

### Bulk Operations
Efficient handling of multiple records:
```csharp
await SaveAll(recordList);
```

## Status

This is currently a **Work in Progress Proof of Concept**. The library demonstrates advanced state management patterns and is being developed as a comprehensive solution for enterprise Blazor applications.

## Contributing

This project is in active development. Contributions, feedback, and suggestions are welcome.

## License

[Add your license information here]
