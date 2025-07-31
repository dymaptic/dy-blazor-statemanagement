# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Blazor state management library for .NET 9 applications. The solution consists of:

- **dymaptic.Blazor.StateManagement** - Core client-side state management library
- **dymaptic.Blazor.StateManagement.Server** - Server-side state management abstractions  
- **dymaptic.Blazor.StateManagement.Sample** - Full-stack Blazor sample application with Identity authentication
- **dymaptic.Blazor.StateManagement.Sample.Client** - Client-side WebAssembly components for the sample

## Architecture

### Core Components

The library provides a state management pattern with these key abstractions:

- **StateRecord** - Base record type for all state entities with `Id`, `CreatedUtc`, `LastUpdatedUtc`
- **IStateManager<T>** - Interface for CRUD operations (Load, Save, Update, Delete, LoadAll)
- **StateComponentBase<T>** - Base Blazor component providing state management with undo/redo functionality
- **CollectionStateComponentBase** - Base component for managing collections of state records

### Client-Server Pattern

- **ClientStateManager<T>** - HTTP client implementation calling REST APIs at `api/state/{typename}`
- **ServerStateManagerBase<T>** - Abstract base class for server-side implementations
- **StateManagementApi** - Server-side API controller base for exposing state endpoints

### Session Storage

- **ISessionStorage** - Abstraction for browser session storage
- **SessionStorage** - Implementation using JavaScript interop for browser storage

## Build and Development Commands

### Build the solution:
```bash
dotnet build
```

### Run the sample application:
```bash
cd sample/dymaptic.Blazor.StateManagement.Sample/dymaptic.Blazor.StateManagement.Sample
dotnet run
```

### Build individual projects:
```bash
# Core library
dotnet build src/dymaptic.Blazor.StateManagement/dymaptic.Blazor.StateManagement.csproj

# Server library  
dotnet build src/dymaptic.Blazor.StateManagement.Server/dymaptic.Blazor.StateManagement.Server.csproj

# Sample application
dotnet build sample/dymaptic.Blazor.StateManagement.Sample/dymaptic.Blazor.StateManagement.Sample/dymaptic.Blazor.StateManagement.Sample.csproj
```

## Key Patterns

### State Component Usage

Components inherit from `StateComponentBase<T>` and get:
- Automatic session storage caching with `ItemCacheKey` and `LastSavedObjectOfTypeKey`
- Undo/redo functionality via `UndoStack` and `RedoStack`
- Authentication integration with `AuthenticationStateProvider`
- CRUD operations: `New()`, `Load()`, `Save()`, `Update()`, `Delete()`
- State tracking with `Update()` method called after each render

### API Endpoints

Server implementations expose REST endpoints following the pattern:
- `GET api/state/{typename}` - Load all records
- `GET api/state/{typename}/{id}` - Load specific record
- `POST api/state/{typename}` - Create new record  
- `PUT api/state/{typename}` - Update existing record
- `DELETE api/state/{typename}/{id}` - Delete record

### Dependencies

- ASP.NET Core 9.0 components and authorization
- Entity Framework Core (in sample)
- Microsoft Extensions Caching Abstractions
- Browser session storage via JavaScript interop

## Sample Application

The sample demonstrates the library usage with:
- ASP.NET Core Identity authentication
- SQLite database with Entity Framework
- Interactive Server and WebAssembly render modes
- Docker support with Linux containers