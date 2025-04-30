# Pizzeria Order Processor - Architecture

## System Overview

The Pizzeria Order Processor is a .NET 9 console application designed to process pizza orders from JSON files, validate them, calculate totals, determine ingredient requirements, and push valid orders to a message queue. The architecture follows clean code principles with a focus on modularity, separation of concerns, and testability.

## Architectural Style

The application follows a **layered architecture** with clear boundaries between components:

```
┌─────────────────────────────────────────────────────────────┐
│                      Configuration Layer                     │
└─────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                        Data Access Layer                     │
│  ┌───────────────┐  ┌────────────────┐  ┌───────────────┐   │
│  │ Product Data  │  │ Ingredient Data│  │   Order Data  │   │
│  └───────────────┘  └────────────────┘  └───────────────┘   │
└─────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                       Business Logic Layer                   │
│  ┌───────────────┐  ┌────────────────┐  ┌───────────────┐   │
│  │   Validation  │  │  Calculation   │  │ Order Assembly│   │
│  └───────────────┘  └────────────────┘  └───────────────┘   │
└─────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                      Integration Layer                       │
│                  ┌────────────────────────┐                  │
│                  │     Queue Interface    │                  │
│                  └────────────────────────┘                  │
└─────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                     Infrastructure Layer                     │
│  ┌───────────────┐  ┌────────────────┐  ┌───────────────┐   │
│  │    Logging    │  │ Error Handling │  │  Mock Queue   │   │
│  └───────────────┘  └────────────────┘  └───────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Configuration Management
- **Settings Classes**: Strongly-typed configuration objects (`FileSettings`, `VatSettings`, `ValidationSettings`, etc.)
- **Configuration Binding**: Uses `Microsoft.Extensions.Configuration` to load settings from `appsettings.json`
- **Dependency Injection**: Configures services using `Microsoft.Extensions.DependencyInjection`

### 2. Data Access Layer
- **ProductCatalogLoader**: Loads product data from JSON files into immutable dictionaries
- **IngredientMappingLoader**: Loads ingredient mappings from JSON files
- **OrderBatchLoader**: Streams and deserializes order data from JSON files

### 3. Business Logic Layer
- **Validation Components**:
  - `RawOrderLineItemValidator`: Validates individual order line items
  - `OrderValidator`: Validates complete orders for consistency
  - `OrderItemValidator`: Validates processed order items
  - `ValidationConstants`: Centralizes validation rules and messages
- **Calculation Components**:
  - `OrderCalculator`: Computes order totals and ingredient requirements
- **Order Processing**:
  - `OrderBatchProcessor`: Orchestrates the validation and processing workflow

### 4. Integration Layer
- **Queue Interface**:
  - `IMockQueue`: Defines the contract for queue interactions
  - `BatchQueuePusher`: Manages batching and pushing orders to the queue

### 5. Infrastructure Layer
- **Logging**:
  - Serilog for structured logging
  - `LogConstants`: Centralizes log messages
- **Error Handling**:
  - Exception handling at appropriate levels
  - Graceful degradation for non-critical failures

## Data Flow

```
┌───────────┐     ┌───────────┐     ┌───────────┐     ┌───────────┐     ┌───────────┐
│  JSON     │     │  Raw      │     │ Validated │     │ Calculated│     │  Queue    │
│  Files    │────▶│  Data     │────▶│  Orders   │────▶│  Orders   │────▶│  Messages │
│           │     │           │     │           │     │           │     │           │
└───────────┘     └───────────┘     └───────────┘     └───────────┘     └───────────┘
```

1. **Input**: JSON files containing order line items
2. **Parsing**: Streaming deserialization into `RawOrderLineItem` objects
3. **Buffering**: Grouping line items by `OrderId`
4. **Validation**: Applying validation rules at item and order levels
5. **Calculation**: Computing totals and ingredient requirements
6. **Queue Pushing**: Batching and sending valid orders to the mock queue

## Key Architectural Patterns

### 1. Dependency Injection
- Constructor injection for all services
- Explicit dependency validation using `ArgumentNullException.ThrowIfNull()`
- Registration of services in a central location

### 2. Repository Pattern
- Data access abstracted through loader classes
- Clear separation between data access and business logic

### 3. Validator Pattern
- Specialized validator classes using FluentValidation
- Separation of validation rules by entity type

### 4. Strategy Pattern
- Pluggable queue implementation via `IMockQueue` interface
- Allows for future replacement with real message brokers

### 5. Builder Pattern
- Order objects constructed progressively through the pipeline
- Clear transformation from raw data to validated orders

### 6. Immutable Data Structures
- `FrozenDictionary` for thread-safe, immutable lookups
- Prevents accidental modification of reference data

## Cross-Cutting Concerns

### 1. Logging
- Structured logging with Serilog
- Consistent log messages via `LogConstants`
- Console and file sinks for comprehensive logging

### 2. Error Handling
- Graceful degradation for non-critical failures
- Appropriate exception propagation
- Detailed error messages for troubleshooting

### 3. Performance Optimization
- Streaming JSON deserialization for memory efficiency
- Efficient data structures for lookups
- Asynchronous I/O operations

### 4. Configuration Management
- Strongly-typed configuration objects
- Centralized settings in `appsettings.json`
- Environment-specific configuration capabilities

## Extensibility Points

The architecture provides several extension points for future enhancements:

1. **Real Message Broker Integration**: Replace `MockQueue` with a real implementation
2. **Parallel Processing**: Add concurrent processing of multiple order files
3. **Web API Layer**: Add an API layer for HTTP-based order submission
4. **Background Processing**: Convert to a background service for continuous processing
5. **Monitoring & Telemetry**: Add distributed tracing and metrics collection

## Deployment Architecture

The application is designed as a standalone console application but could be extended to various deployment models:

1. **Containerized Microservice**: Package as a Docker container
2. **Scheduled Job**: Run as a scheduled task or cron job
3. **Event-Driven Service**: Trigger processing based on file system events
4. **API Backend**: Add HTTP endpoints and deploy as a web service

## Security Considerations

1. **Input Validation**: Thorough validation of all input data
2. **Error Information**: Careful control of error details exposed to users
3. **Logging**: No sensitive information in logs
4. **Configuration**: Secure handling of configuration values

## Conclusion

The Pizzeria Order Processor architecture emphasizes modularity, testability, and maintainability. The clear separation of concerns and well-defined interfaces allow for future extensions and modifications without significant rework. The use of modern .NET features and design patterns ensures the application is robust, efficient, and aligned with industry best practices.
