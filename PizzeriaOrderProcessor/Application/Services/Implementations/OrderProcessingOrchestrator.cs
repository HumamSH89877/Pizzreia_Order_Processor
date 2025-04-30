using System.Collections.Frozen;
using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using ValidationResult = FluentValidation.Results.ValidationResult;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;
using System.IO;
using PizzeriaOrderProcessor.Application.Services.Interfaces;

namespace PizzeriaOrderProcessor.Application.Services.Implementations
{
    public class OrderProcessingOrchestrator : IOrderProcessingOrchestrator
    {
        private readonly IOrderBatchProcessor _orderBatchProcessor;
        private readonly FrozenDictionary<string, Product> _productCatalog;
        private readonly FrozenDictionary<string, List<IngredientInfo>> _ingredientMappings;
        private readonly IValidator<Order> _orderValidator;
        private readonly IOrderCalculator _orderCalculator;
        private readonly IIngredientAggregator _ingredientAggregator;
        private readonly IBatchQueuePusher _batchQueuePusher;
        private readonly ISummaryReporter _summaryReporter;
        private readonly ILogger<OrderProcessingOrchestrator> _logger;
        private readonly int _queueBatchSize;

        private const string UNKNOWN_FILE = "UnknownFile";

        public class OrderProcessingDependencies
        {
            public required FrozenDictionary<string, Product> ProductCatalog { get; set; }
            public required FrozenDictionary<string, List<IngredientInfo>> IngredientMappings { get; set; }
            public required IValidator<Order> OrderValidator { get; set; }
            public required IOrderCalculator OrderCalculator { get; set; }
            public required IIngredientAggregator IngredientAggregator { get; set; }
            public required IBatchQueuePusher BatchQueuePusher { get; set; }
            public required ISummaryReporter SummaryReporter { get; set; }
        }

        public OrderProcessingOrchestrator(
            IOrderBatchProcessor orderBatchProcessor,
            OrderProcessingDependencies dependencies,
            IOptions<QueueSettings> queueOptions,
            ILogger<OrderProcessingOrchestrator> logger)
        {
            _orderBatchProcessor = orderBatchProcessor;
            _productCatalog = dependencies.ProductCatalog;
            _ingredientMappings = dependencies.IngredientMappings;
            _orderValidator = dependencies.OrderValidator;
            _orderCalculator = dependencies.OrderCalculator;
            _ingredientAggregator = dependencies.IngredientAggregator;
            _batchQueuePusher = dependencies.BatchQueuePusher;
            _summaryReporter = dependencies.SummaryReporter;
            _logger = logger;
            _queueBatchSize = queueOptions.Value.BatchSize > 0 ? queueOptions.Value.BatchSize : 50;
            _logger.LogInformation("OrderProcessingOrchestrator initialized.");
        }

        public async Task ProcessOrderBatchAsync(IEnumerable<string> inputFilePaths, CancellationToken cancellationToken)
        {
            if (inputFilePaths == null) throw new ArgumentNullException(nameof(inputFilePaths));
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting order batch processing for {FileCount} file(s)...", inputFilePaths.Count());

            var processingContext = new OrderProcessingContext
            {
                ValidOrders = new List<Order>(),
                InvalidOrderResults = new Dictionary<string, ValidationResult>(),
                InvalidOrderSourceFiles = new Dictionary<string, string>(),
                ValidOrderSourceFiles = new Dictionary<string, string>(),
                IngredientTotals = new Dictionary<string, decimal>()
            };
            
            long skippedLineItemCount = 0;
            Dictionary<string, List<BufferedLineItemInfo>> bufferedLines;

            try
            {
                // --- Phase 1: Buffering ---
                _logger.LogInformation("1. Buffering order files... 0%");
                try
                {
                    var result = await _orderBatchProcessor.BufferOrderLinesAsync(inputFilePaths!, cancellationToken);
                    bufferedLines = result.Buffer;
                    skippedLineItemCount = result.SkippedCount;
                    _logger.LogInformation("1. Buffering order files... 100%");
                    _logger.LogInformation("File buffering complete. Buffered {OrderCount} unique OrderIds. Skipped {SkippedCount} items.", 
                        bufferedLines.Count, skippedLineItemCount);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Fatal error during file buffering phase. Aborting processing.");
                    throw new InvalidOperationException("Failed to buffer order files for processing", ex);
                }

                if (ShouldAbortProcessing(bufferedLines, cancellationToken)) return;

                // --- Phase 2: Processing Buffered Orders ---
                await ProcessBufferedOrdersAsync(bufferedLines, processingContext, cancellationToken);

                // --- Phase 3: Push Valid Orders to Queue ---
                if (processingContext.ValidOrders.Any() && !cancellationToken.IsCancellationRequested)
                {
                    await PushOrdersToQueueAsync(processingContext.ValidOrders, cancellationToken);
                }

                // --- Phase 4: Ingredient Aggregation ---
                if (processingContext.ValidOrders.Any())
                {
                    AggregateIngredients(processingContext);
                }
                else
                {
                    _logger.LogInformation("Skipping ingredient aggregation as there are no valid orders.");
                }
            }
            catch (Exception ex) // Catch unexpected errors during the overall orchestration
            {
                _logger.LogCritical(ex, "Unhandled exception during batch processing orchestration.");           
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation("Batch processing finished in {ElapsedMilliseconds:N0} ms.", stopwatch.ElapsedMilliseconds);

                _logger.LogInformation("Displaying final summary...");
                // Ensure collected data is passed, even if process was interrupted
                _summaryReporter.DisplaySummary(
                    processingContext.ValidOrders, 
                    processingContext.InvalidOrderResults, 
                    processingContext.IngredientTotals, 
                    skippedLineItemCount, 
                    processingContext.InvalidOrderSourceFiles, 
                    processingContext.ValidOrderSourceFiles);
            }
        }

        private bool ShouldAbortProcessing(Dictionary<string, List<BufferedLineItemInfo>> bufferedLines, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            { 
                _logger.LogWarning("Cancellation requested after buffering.");
                return true;
            }
            
            if (bufferedLines == null || !bufferedLines.Any())
            { 
                _logger.LogInformation("No orders buffered. Skipping further processing.");
                return true;
            }
            
            return false;
        }

        private async Task ProcessBufferedOrdersAsync(
            Dictionary<string, List<BufferedLineItemInfo>> bufferedLines,
            OrderProcessingContext context,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting processing of {OrderCount} buffered orders...", bufferedLines.Count);
            _logger.LogInformation("2. Processing buffered orders... 0.0%");
            
            int processedCount = 0;
            int totalBufferedOrders = bufferedLines.Count;

            foreach (var kvp in bufferedLines)
            {
                if (cancellationToken.IsCancellationRequested)
                { 
                    _logger.LogWarning("Cancellation requested during order processing.");
                    break;
                }

                string orderId = kvp.Key;
                List<BufferedLineItemInfo> bufferedInfoList = kvp.Value;
                string sourceFileName = bufferedInfoList.FirstOrDefault()?.SourceFileName ?? UNKNOWN_FILE;

                // Extract the RawOrderLineItem objects from the BufferedLineItemInfo records
                List<RawOrderLineItem> linesForValidation = bufferedInfoList.Select(info => info.LineItem).ToList();

                (Order? reconstructedOrder, ValidationResult? preValidationError) =
                    TryReconstructAndPreValidateOrder(orderId, linesForValidation);

                if (reconstructedOrder == null)
                {
                    // Handle invalid order
                    context.InvalidOrderResults.Add(orderId, preValidationError ?? 
                        CreateErrorResult("Unknown pre-validation error (reconstruction failed)."));
                    context.InvalidOrderSourceFiles[orderId] = sourceFileName;
                    _logger.LogWarning("Order {OrderId} failed pre-validation in file {FileName}: {ErrorMessage}", 
                        orderId, sourceFileName, preValidationError?.ToString());
                }
                else
                {
                    // Process valid order
                    await ValidateAndProcessOrderAsync(reconstructedOrder, orderId, sourceFileName, context, cancellationToken);
                }

                // Update progress
                processedCount++;
                double percentage = (double)processedCount / totalBufferedOrders * 100.0;
                _logger.LogInformation("2. Processing buffered orders... {Percentage:F1}% complete.", percentage);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Order processing phase complete.");
            }
        }

        private async Task ValidateAndProcessOrderAsync(
            Order order, 
            string orderId, 
            string sourceFileName, 
            OrderProcessingContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                _orderCalculator.CalculateOrderTotals(order);
                var validationResult = await _orderValidator.ValidateAsync(order, cancellationToken);

                if (validationResult.IsValid)
                {
                    context.ValidOrders.Add(order);
                    context.ValidOrderSourceFiles[orderId] = sourceFileName;
                }
                else
                {
                    context.InvalidOrderResults.Add(orderId, validationResult);
                    context.InvalidOrderSourceFiles[orderId] = sourceFileName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Order {OrderId}. Marking as invalid. Error: {ErrorMessage}", 
                    orderId, ex.Message);
                context.InvalidOrderResults[orderId] = new ValidationResult(
                    new[] { new ValidationFailure("*", $"Internal processing error: {ex.Message}") });
                context.InvalidOrderSourceFiles[orderId] = sourceFileName;
            }
        }

        private async Task PushOrdersToQueueAsync(List<Order> validOrders, CancellationToken cancellationToken)
        {
            int numBatches = (int)Math.Ceiling((double)validOrders.Count / _queueBatchSize);
            _logger.LogInformation("Starting push of {ValidOrderCount} valid orders to queue in {NumBatches} batches...", 
                validOrders.Count, numBatches);

            try
            {
                await _batchQueuePusher.PushOrdersToQueueAsync(validOrders, cancellationToken);
                _logger.LogInformation("Queue push attempts complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during the queue pushing process (outside individual batch failures)");
                throw new InvalidOperationException("Failed to push orders to queue", ex);
            }
        }

        private void AggregateIngredients(OrderProcessingContext context)
        {
            _logger.LogInformation("Aggregating ingredients for {ValidOrderCount} valid orders...", context.ValidOrders.Count);
            context.IngredientTotals = _ingredientAggregator.AggregateIngredients(context.ValidOrders);
            _logger.LogInformation("Ingredient aggregation complete. Found {IngredientCount} unique ingredients.", 
                context.IngredientTotals.Count);
        }

        // Context class to reduce parameter passing
        private sealed class OrderProcessingContext
        {
            public List<Order> ValidOrders { get; set; } = new();
            public Dictionary<string, ValidationResult> InvalidOrderResults { get; set; } = new();
            public Dictionary<string, string> InvalidOrderSourceFiles { get; set; } = new();
            public Dictionary<string, string> ValidOrderSourceFiles { get; set; } = new();
            public Dictionary<string, decimal> IngredientTotals { get; set; } = new();
        }

        // Helper: TryReconstructAndPreValidateOrder 
         private (Order? Order, ValidationResult? ErrorResult) TryReconstructAndPreValidateOrder(
            string orderId,
            List<RawOrderLineItem> lines)
        {
            if (lines == null || !lines.Any())
            {
                return (null, CreateErrorResult($"No line items found for this Order ID"));
            }

            // 1. Consistency Checks
            var consistencyCheckResult = CheckOrderConsistency(orderId, lines);
            if (consistencyCheckResult.HasErrors)
            {
                return (null, consistencyCheckResult.ErrorResult);
            }

            // Assume non-nullable if consistency check passes and lines exist
            var order = new Order
            {
                OrderId = orderId,
                DeliverAt = consistencyCheckResult.DeliverAt!.Value,
                CreatedAt = consistencyCheckResult.CreatedAt!.Value,
                CustomerAddress = consistencyCheckResult.Address ?? string.Empty, 
                Items = new List<OrderItem>()
            };

            // 2. Referential Integrity & Item Construction
            var itemValidationResult = ValidateAndAddOrderItems(orderId, lines, order);
            if (itemValidationResult.HasErrors)
            {
                return (null, itemValidationResult.ErrorResult);
            }

            // If we added no items (e.g., all lines had issues), fail
            if (!order.Items.Any())
            {
                return (null, CreateErrorResult($"Order reconstruction resulted in no valid items after checking products/mappings"));
            }

            _logger.LogDebug("Order {OrderId} reconstructed and passed pre-validation checks.", orderId);
            return (order, null); // Success
        }

        private (bool HasErrors, ValidationResult? ErrorResult, DateTime? DeliverAt, DateTime? CreatedAt, string? Address) 
            CheckOrderConsistency(string orderId, List<RawOrderLineItem> lines)
        {
            var firstLine = lines[0];
            DateTime? consistentDeliverAt = firstLine.DeliverAt;
            DateTime? consistentCreatedAt = firstLine.CreatedAt;
            string? consistentAddress = firstLine.CustomerAddress;
            string referenceProductId = firstLine.ProductId ?? "Unknown";
            var consistencyErrors = new List<ValidationFailure>();

            foreach (var line in lines.Skip(1))
            {
                CheckLineConsistency(
                    line, 
                    consistentDeliverAt, 
                    consistentCreatedAt, 
                    consistentAddress, 
                    referenceProductId, 
                    orderId, 
                    consistencyErrors);
            }
            
            if (consistencyErrors.Any())
            {
                string errorMessages = string.Join(", ", consistencyErrors.Select(e => e.ErrorMessage));
                return (true, CreateErrorResult($"Inconsistent order data: {errorMessages}"), null, null, null);
            }

            return (false, null, consistentDeliverAt, consistentCreatedAt, consistentAddress);
        }

        private static void CheckLineConsistency(
            RawOrderLineItem line, 
            DateTime? consistentDeliverAt, 
            DateTime? consistentCreatedAt, 
            string? consistentAddress, 
            string referenceProductId, 
            string orderId, 
            List<ValidationFailure> consistencyErrors)
        {
            if (line.DeliverAt != consistentDeliverAt)
                consistencyErrors.Add(new ValidationFailure(nameof(Order.DeliverAt), 
                    $"Order {orderId}: Inconsistent DeliverAt times for product {referenceProductId} ({consistentDeliverAt}) vs product {line.ProductId} ({line.DeliverAt})."));
            
            if (line.CreatedAt != consistentCreatedAt)
                consistencyErrors.Add(new ValidationFailure(nameof(Order.CreatedAt), 
                    $"Order {orderId}: Inconsistent CreatedAt times for product {referenceProductId} ({consistentCreatedAt}) vs product {line.ProductId} ({line.CreatedAt})."));
            
            if (line.CustomerAddress != consistentAddress)
                consistencyErrors.Add(new ValidationFailure(nameof(Order.CustomerAddress), 
                    $"Order {orderId}: Inconsistent CustomerAddress for product {referenceProductId} vs product {line.ProductId}."));
        }

        private (bool HasErrors, ValidationResult? ErrorResult) ValidateAndAddOrderItems(
            string orderId, 
            List<RawOrderLineItem> lines, 
            Order order)
        {
            var itemErrors = new List<ValidationFailure>();
            
            foreach (var line in lines)
            {
                ValidateAndAddOrderItem(line, order, orderId, itemErrors);
            }

            // If any item errors occurred during iteration, fail the order reconstruction
            if (itemErrors.Count > 0)
            {
                string errorMessages = string.Join(", ", itemErrors.Select(e => e.ErrorMessage));
                return (true, CreateErrorResult($"Invalid items: {errorMessages}"));
            }

            return (false, null);
        }

        private void ValidateAndAddOrderItem(
            RawOrderLineItem line, 
            Order order, 
            string orderId, 
            List<ValidationFailure> itemErrors)
        {
            // Check Product Exists
            if (string.IsNullOrWhiteSpace(line.ProductId)) 
            {
                itemErrors.Add(new ValidationFailure($"{nameof(OrderItem.ProductId)}", 
                    $"Line item found with missing ProductId for Order {orderId}."));
                return;
            }
            
            if (!_productCatalog.TryGetValue(line.ProductId, out var product))
            {
                itemErrors.Add(new ValidationFailure($"{nameof(OrderItem.ProductId)}[{line.ProductId}]", 
                    $"ProductId '{line.ProductId}' not found in Product Catalog for Order {orderId}."));
                return;
            }

            // Check Ingredient Mapping Exists
            if (!_ingredientMappings.ContainsKey(line.ProductId))
            {
                itemErrors.Add(new ValidationFailure($"{nameof(OrderItem.ProductId)}[{line.ProductId}]", 
                    $"Ingredient mapping not found for ProductId '{line.ProductId}' for Order {orderId}."));
                return;
            }

            // Basic Quantity Check (minimal here, full check in validator)
            if (line.Quantity <= 0)
            {
                itemErrors.Add(new ValidationFailure($"{nameof(OrderItem.Quantity)}[{line.ProductId}]", 
                    $"Line item for ProductId '{line.ProductId}' has a quantity of ({line.Quantity}) for Order {orderId}."));
                return;
            }

            // If all checks pass for this line item, add it
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                AssociatedProduct = product
            });
        }

        // Helper to create a basic ValidationResult for pre-validation/reconstruction errors
        private ValidationResult CreateErrorResult(string errorMessage, string propertyName = "")
        {
             // Use a distinct property name or convention for reconstruction errors
             var failure = new ValidationFailure(propertyName, errorMessage)
             {
                 ErrorCode = "ReconstructionFailure" // Custom error code
             };
            
             return new ValidationResult(new[] { failure });
        }
    }
} 