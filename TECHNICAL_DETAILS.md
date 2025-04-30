# Technical Details

## Application Workflow
1. **Configuration Binding**: Reads `appsettings.json` via `Microsoft.Extensions.Configuration` into strongly-typed settings objects (e.g., `FileSettings`, `VatSettings`, `ValidationSettings`).
2. **Dependency Injection Setup**: Configures services like loaders, validators, calculators, and the mock queue using `Microsoft.Extensions.DependencyInjection`.
3. **Constructor Validation**: Key services rigorously validate constructor parameters for null using `ArgumentNullException.ThrowIfNull()`.
4. **Data Loading**:
   - `ProductCatalogLoader` deserializes `products.json` into an immutable `FrozenDictionary<string, Product>`.
   - `IngredientMappingLoader` loads `ingredients.json` into a `FrozenDictionary<string, List<IngredientInfo>>`.
   - `OrderBatchLoader` asynchronously streams each input JSON order file via `JsonSerializer.DeserializeAsyncEnumerable<RawOrderLineItem>`, buffering line items by `OrderId`.
5. **Validation**:
   - `RawOrderLineItemValidator` enforces per-item rules using centralized patterns and messages from `ValidationConstants`:
     - `OrderId`: Must match pattern `^ORD-\d{5}$`
     - `ProductId`: Must match pattern `^PRD-\d{5}$`
     - `Quantity`: Must be between 1 and configured maximum
     - Date fields: Must be valid and within operating hours
     - `CustomerAddress`: Must not be empty and follow address format
   - After buffering, `OrderValidator` checks order-level consistency (`CreatedAt`, `DeliverAt`, `CustomerAddress`), applies operating hours rules, minimum order total, and uses `OrderItemValidator`. All validators leverage `ValidationConstants` for DRY compliance.
6. **Calculation**:
   - `OrderCalculator` computes line totals (product price + VAT), aggregates per-order totals, and calculates ingredient requirements for valid orders.
7. **Queue Interaction**:
   - `BatchQueuePusher` splits valid orders into sub-batches (`QueueSettings.PushBatchSize`) and pushes them via `MockQueue.PushBatchAsync`.
   - Simulated transient failures are caught per sub-batch, logged using `LogConstants` templates, and processing continues.

## Design Decisions & Trade-offs
- **Streaming vs In-memory**: Streaming JSON avoids high memory usage for large files but requires buffering line items per `OrderId` to perform order-level validation. The buffer stores minimal info (`BufferedLineItemInfo`) to balance memory and validation needs.
- **Immutable Data**: `FrozenDictionary` provides fast, thread-safe lookups for product/ingredient data, beneficial for potential future parallelism.
- **Centralized Constants**: `ValidationConstants` and `LogConstants` classes enforce DRY principles, improve maintainability, and ensure consistent messaging/patterns across the application.
- **Batch Processing**: Configurable sub-batches for queue pushing localize the impact of transient failures.
- **Mock Queue Abstraction**: Uses `IMockQueue` interface to simulate a real message broker, allowing easy replacement in the future.
- **Explicit Constructor Validation**: Using `ArgumentNullException.ThrowIfNull` ensures services receive valid dependencies, preventing `NullReferenceException` later.

## Error Handling
- **Configuration & File Errors**: Critical errors during loading of essential files (`products.json`, `ingredients.json`) or configuration binding cause the application to log the error and exit gracefully.
- **Order File Errors**: Malformed JSON within an order file results in that specific file being skipped, logged (using `LogConstants`), and processing continues with other files.
- **Validation Errors**: Captured via FluentValidation using rules defined with `ValidationConstants`. Invalid orders or line items are skipped, logged with specific reasons, and listed in the summary.
- **Queue Failures**: Transient errors during `MockQueue.PushBatchAsync` are logged per sub-batch (including batch index and exception details), and subsequent batches are still attempted.
- **Logging**: Serilog provides structured logging to the console and daily rolling files (`./logs/pizzeria-.log`), leveraging `LogConstants` for consistent messages.

## Performance & Scalability
- **Async Operations**: Primarily uses `async/await` for I/O (file reading, queue pushing) to avoid blocking threads.
- **Efficient Data Structures**: `FrozenDictionary` for catalog lookups; `Dictionary` for buffering.
- **Streaming Deserialization**: `JsonSerializer.DeserializeAsyncEnumerable` processes large order files item by item.
- **Latency Consideration**: The primary latency is file I/O and JSON deserialization/validation. Buffering adds minimal overhead. Queue pushing is currently mocked but would be a network latency factor in a real system.
- **Batch Size Tuning**: `QueueSettings.PushBatchSize` offers a way to balance queue throughput and memory usage during the push phase.
- **Sequential Processing**: Currently processes files sequentially; a parallelism approach would require thread-safety considerations in buffering and validation.

## Testing & Development Workflow
- **Unit Testing**: Comprehensive tests cover loaders, validators, calculators, and queue pushing logic using xUnit and Moq.
- **Test Data**: Uses distinct test data reflecting valid/invalid scenarios and edge cases.
- **TDD Influence**: Modular design and high test coverage suggest a Test-Driven Development influence.
- **Code Style**: Adheres to standard C# conventions, utilizes modern features (`ArgumentNullException.ThrowIfNull`), and enforces DRY principles.

## Future Improvements
- Integrate a real message broker (RabbitMQ, Azure Service Bus) by implementing `IMockQueue`.
- Implement parallel processing for loading/validating multiple order files concurrently.
- Add more sophisticated retry logic for queue pushing with exponential backoff.
- Introduce distributed tracing and metrics (Prometheus, App Insights) for monitoring in a production environment.
- Consider using a dedicated mapping library like AutoMapper if object transformations become more complex.
- Explore asynchronous validation to improve throughput for large order volumes.
- Use `MediatR` for in-process command/query handling.
- Support parallel validation/calculation for large order volumes.
- Expose a web API or background service for continuous processing.
