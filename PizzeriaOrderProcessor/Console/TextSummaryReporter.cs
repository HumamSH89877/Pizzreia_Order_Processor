using ValidationResult = FluentValidation.Results.ValidationResult;
using PizzeriaOrderProcessor.Domain;
using Microsoft.Extensions.Logging;
using PizzeriaOrderProcessor.Application.Services.Interfaces;

namespace PizzeriaOrderProcessor.Console
{
    public class TextSummaryReporter : ISummaryReporter
    {
        private readonly ILogger<TextSummaryReporter> _logger;
        private const string Separator = "==============================================================";
        private const string ShortSeparator = "--------------------------------------------------------------";

        public TextSummaryReporter(ILogger<TextSummaryReporter> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        public void DisplaySummary(
            IEnumerable<Order> processedOrders,
            IReadOnlyDictionary<string, ValidationResult> invalidOrderResults,
            IReadOnlyDictionary<string, decimal> ingredientTotals,
            long skippedLineItemCount,
            IReadOnlyDictionary<string, string>? invalidOrderSourceFiles = null,
            IReadOnlyDictionary<string, string>? validOrderSourceFiles = null)
        {
            var processedOrdersList = processedOrders?.ToList() ?? new List<Order>();
            var invalidOrdersDict = invalidOrderResults ?? new Dictionary<string, ValidationResult>();
            var ingredientTotalsDict = ingredientTotals ?? new Dictionary<string, decimal>();

            DisplayHeader();
            DisplaySummaryStatistics(processedOrdersList.Count, invalidOrdersDict.Count, skippedLineItemCount);
            DisplayValidOrders(processedOrdersList, validOrderSourceFiles);
            DisplayInvalidOrders(invalidOrdersDict, invalidOrderSourceFiles);
            DisplayIngredientTotals(ingredientTotalsDict);
            DisplayFooter(skippedLineItemCount);
        }

        private void DisplayHeader()
        {
            _logger.LogInformation(Separator);
            _logger.LogInformation("BATCH PROCESSING SUMMARY");
            _logger.LogInformation(Separator);
            _logger.LogInformation("");
        }

        private void DisplaySummaryStatistics(int validOrderCount, int invalidOrderCount, long skippedLineItemCount)
        {
            _logger.LogInformation("SUMMARY STATISTICS");
            _logger.LogInformation("  Total Orders Processed: {TotalCount}", validOrderCount + invalidOrderCount);
            _logger.LogInformation("  Valid Orders: {ValidCount}", validOrderCount);
            _logger.LogInformation("  Invalid Orders: {InvalidCount}", invalidOrderCount);
            _logger.LogInformation("  Skipped Line Items: {SkippedCount}", skippedLineItemCount);
            _logger.LogInformation("");
        }

        private void DisplayValidOrders(
            List<Order> processedOrdersList,
            IReadOnlyDictionary<string, string>? validOrderSourceFiles)
        {
            _logger.LogInformation(Separator);
            _logger.LogInformation("VALID ORDERS ({ValidCount})", processedOrdersList.Count);
            _logger.LogInformation(Separator);
            
            if (!processedOrdersList.Any())
            {
                _logger.LogInformation("  No orders were successfully processed and validated.");
                _logger.LogInformation(ShortSeparator);
                _logger.LogInformation("");
                return;
            }

            foreach (var order in processedOrdersList.OrderBy(o => o.OrderId))
            {
                DisplayValidOrder(order, validOrderSourceFiles);
            }
            
            _logger.LogInformation("");
        }

        private void DisplayValidOrder(
            Order order, 
            IReadOnlyDictionary<string, string>? validOrderSourceFiles)
        {
            _logger.LogInformation("  Order ID: {OrderId}", order.OrderId);
            
            // Add source file information if available
            if (validOrderSourceFiles != null && validOrderSourceFiles.TryGetValue(order.OrderId, out var sourceFile))
            {
                _logger.LogInformation("  Source File: {SourceFile}", sourceFile);
            }
            
            _logger.LogInformation("  Total Amount: {TotalAmount:N2} AED", order.TotalAmountIncludingVat);
            _logger.LogInformation("  Items: {ItemCount}", order.Items?.Count ?? 0);
            
            DisplayOrderItems(order);
            
            _logger.LogInformation("  Delivery Time: {DeliveryTime}", order.DeliverAt.ToString("yyyy-MM-dd HH:mm:ss"));
            _logger.LogInformation(ShortSeparator);
        }

        private void DisplayOrderItems(Order order)
        {
            if (order.Items == null || !order.Items.Any())
            {
                return;
            }
            
            foreach (var item in order.Items)
            {
                string productName = item.AssociatedProduct?.ProductName ?? item.ProductId;
                _logger.LogInformation("    * {Quantity} × {ProductName}", item.Quantity, productName);
            }
        }

        private void DisplayInvalidOrders(
            IReadOnlyDictionary<string, ValidationResult> invalidOrdersDict,
            IReadOnlyDictionary<string, string>? invalidOrderSourceFiles)
        {
            _logger.LogInformation(Separator);
            _logger.LogInformation("INVALID ORDERS ({InvalidCount})", invalidOrdersDict.Count);
            _logger.LogInformation(Separator);
            
            if (!invalidOrdersDict.Any())
            {
                _logger.LogInformation("  No orders were found to be invalid during processing.");
                _logger.LogInformation(ShortSeparator);
                _logger.LogInformation("");
                return;
            }

            foreach (var kvp in invalidOrdersDict.OrderBy(kv => kv.Key))
            {
                DisplayInvalidOrder(kvp.Key, kvp.Value, invalidOrderSourceFiles);
            }
            
            _logger.LogInformation("");
            _logger.LogInformation("  Check logs for validation failures");
            _logger.LogInformation("");
        }

        private void DisplayInvalidOrder(
            string orderId, 
            ValidationResult validationResult, 
            IReadOnlyDictionary<string, string>? invalidOrderSourceFiles)
        {
            string sourceFile = "Unknown";
            if (invalidOrderSourceFiles != null && invalidOrderSourceFiles.TryGetValue(orderId, out var fileName))
            {
                sourceFile = fileName;
            }
            
            // Show order ID in both console and log file
            _logger.LogInformation("  Order ID: {OrderId} (Source: {SourceFile})", orderId, sourceFile);
            
            // These messages will only go to the log file based on our Serilog filter
            using (var scope = _logger.BeginScope("{ValidationDetails}", true))
            {
                _logger.LogInformation("  Validation Errors:");
                
                if (validationResult?.Errors == null || !validationResult.Errors.Any())
                {
                    _logger.LogInformation("    * No specific errors reported (Order might have failed pre-validation).");
                }
                else
                {
                    foreach (var error in validationResult.Errors.OrderBy(e => e.PropertyName).ThenBy(e => e.ErrorMessage))
                    {
                        string propertyPrefix = string.IsNullOrWhiteSpace(error.PropertyName) ? "" : $"{error.PropertyName}: ";
                        _logger.LogInformation("    * {ErrorMessage}", $"{propertyPrefix}{error.ErrorMessage}");
                    }
                }
                
                _logger.LogInformation(ShortSeparator);
            }
        }

        private void DisplayIngredientTotals(IReadOnlyDictionary<string, decimal> ingredientTotalsDict)
        {
            _logger.LogInformation(Separator);
            _logger.LogInformation("REQUIRED INGREDIENTS");
            _logger.LogInformation(Separator);
            
            if (!ingredientTotalsDict.Any())
            {
                _logger.LogInformation("  No ingredients were required (no valid orders processed or no mappings found).");
                _logger.LogInformation(ShortSeparator);
                _logger.LogInformation("");
                return;
            }

            // Find the longest ingredient name for alignment
            int maxNameLength = ingredientTotalsDict.Keys.Max(k => k.Length);
            
            foreach (var kvp in ingredientTotalsDict.OrderBy(i => i.Key, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogInformation("  {IngredientName}: {Amount,10:N2} grams", 
                    kvp.Key.PadRight(maxNameLength), kvp.Value);
            }
            
            _logger.LogInformation(ShortSeparator);
            _logger.LogInformation("");
        }

        private void DisplayFooter(long skippedLineItemCount)
        {
            if (skippedLineItemCount > 0)
            {
                _logger.LogInformation("NOTE: {SkippedCount} line item(s) were skipped during file parsing.",
                    skippedLineItemCount);
                _logger.LogInformation("      Check logs for details on skipped items.");
                _logger.LogInformation("");
            }

            _logger.LogInformation(Separator);
            _logger.LogInformation("END OF SUMMARY");
            _logger.LogInformation(Separator);
        }
    }
}
