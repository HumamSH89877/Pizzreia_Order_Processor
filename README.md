# Pizzeria Order Processor

## Overview
A .NET 9 console application for processing pizzeria orders: loads sample data, parses JSON orders, validates them, calculates totals and ingredients, simulates queue pushes, and presents a polished console UX.

## Architecture & Design
- **Modular Layers**: Configuration → Data Loading → Validation → Calculation → Queue Interaction → Console UI.
- **Streaming I/O**: Async JSON streaming of order line items to minimize memory.
- **Immutable Catalogs**: `FrozenDictionary` for fast, thread-safe lookups.
- **Batch Processing**: `BatchQueuePusher` splits valid orders and handles transient failures.
- **Logging**: Serilog with console sink for structured logs.

## Prerequisites
- .NET 9 SDK

## Build & Run
```powershell
# Restore & build
dotnet build

# Run with all sample orders
dotnet run -- SampleOrders/*.json

# Or specify individual files:
dotnet run -- orders/order1.json orders/order2.json
```

## Configuration
Settings are in `appsettings.json` under sections:
- **FileSettings**: `ProductFilePath`, `IngredientFilePath`
- **VatSettings**, **ValidationSettings**, **TimeSettings**, **OperatingHours**
- **QueueSettings**: `PushBatchSize`

## Assumptions
- Relative paths are resolved against the current working directory.
- Orders must have consistent `CreatedAt`, `DeliverAt`, and `CustomerAddress` per `OrderId`.
- Minimum order amount and quantity limits are enforced from `appsettings.json`.
- Malformed JSON in any order file aborts processing: the error is logged and the application exits.

## Dependencies (NuGet)
- **Serilog**, **Serilog.Sinks.Console**, **Serilog.Settings.Configuration**, **Serilog.Sinks.File**: logging
- **FluentValidation**: validation rules
- **System.Collections.Frozen**: immutable dictionaries
- **Spectre.Console**: rich console progress bars & tables
- **Microsoft.Extensions.Configuration**: settings binding

## Related Concepts
- **MediatR** for in-process messaging/CQRS
- **AutoMapper** for object mapping
- Replace `MockQueue` with real brokers (RabbitMQ, Azure Service Bus)
- Extend to a web API or background worker

## Sample Data
- `products.json`, `ingredients.json` in project root
- 20 sample order files in `SampleOrders/`

## Testing
- 57 unit tests covering validation, calculation, queue logic
```powershell
dotnet test
```

## License & Contact
MIT License. Questions or feedback: humam@example.com
