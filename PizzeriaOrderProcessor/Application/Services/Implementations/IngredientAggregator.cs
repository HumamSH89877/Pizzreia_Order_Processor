using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using PizzeriaOrderProcessor.Application.Services.Interfaces;
using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Implementations
{
    public class IngredientAggregator : IIngredientAggregator
    {
        private readonly ILogger<IngredientAggregator> _logger;
        private readonly FrozenDictionary<string, List<IngredientInfo>> _ingredientMappings;

        public IngredientAggregator(
            FrozenDictionary<string, List<IngredientInfo>> ingredientMappings,
            ILogger<IngredientAggregator> logger)
        {
            _ingredientMappings = ingredientMappings;
            _logger = logger;
        }

        public Dictionary<string, decimal> AggregateIngredients(IEnumerable<Order> validOrders)
        {
            var ingredientTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var orderList = validOrders?.ToList() ?? new List<Order>();

            if (!orderList.Any())
            {
                _logger.LogInformation("No valid orders provided for ingredient aggregation.");
                return ingredientTotals; 
            }

            _logger.LogInformation("Starting ingredient aggregation for {OrderCount} valid orders.", orderList.Count);

            foreach (var order in orderList)
            {
                ProcessOrderIngredients(order, ingredientTotals);
            }

            _logger.LogInformation("Finished ingredient aggregation. Found {IngredientCount} unique ingredients.", ingredientTotals.Count);
            return ingredientTotals;
        }

        private void ProcessOrderIngredients(Order? order, Dictionary<string, decimal> ingredientTotals)
        {
            if (order?.Items == null) return; // Skip null orders/items

            foreach (var item in order.Items)
            {
                ProcessOrderItem(item, order.OrderId, ingredientTotals);
            }
        }

        private void ProcessOrderItem(OrderItem? item, string orderId, Dictionary<string, decimal> ingredientTotals)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ProductId)) return; // Skip null items or those without product ID

            // Look up the product in the pre-loaded ingredient mappings
            if (!_ingredientMappings.TryGetValue(item.ProductId, out var ingredientsForProduct))
            {
                _logger.LogError("Ingredient mapping unexpectedly not found for ProductId {ProductId} in supposedly valid Order {OrderId}. This indicates an inconsistency.", item.ProductId, orderId);
                return;
            }

            if (ingredientsForProduct == null || !ingredientsForProduct.Any())
            {
                // This product exists in mapping but has no ingredients listed
                return; 
            }

            ProcessIngredientsForProduct(ingredientsForProduct, item, orderId, ingredientTotals);
        }

        private void ProcessIngredientsForProduct(
            List<IngredientInfo> ingredientsForProduct, 
            OrderItem item, 
            string orderId, 
            Dictionary<string, decimal> ingredientTotals)
        {
            foreach (var ingredientInfo in ingredientsForProduct)
            {
                ProcessSingleIngredient(ingredientInfo, item, orderId, ingredientTotals);
            }
        }

        private void ProcessSingleIngredient(
            IngredientInfo? ingredientInfo, 
            OrderItem item, 
            string orderId, 
            Dictionary<string, decimal> ingredientTotals)
        {
            // Defensive check on loaded data
            if (ingredientInfo == null || string.IsNullOrWhiteSpace(ingredientInfo.IngredientName) || ingredientInfo.Amount <= 0)
            {
                _logger.LogWarning("Skipping invalid ingredient data (Name: '{Name}', Amount: {Amount}) found in mapping for ProductId {ProductId}.",
                    ingredientInfo?.IngredientName ?? "NULL", ingredientInfo?.Amount ?? 0, item.ProductId);
                return;
            }

            try
            {
                AddIngredientToTotals(ingredientInfo, item.Quantity, ingredientTotals);
            }
            catch (OverflowException ex)
            {
                _logger.LogCritical(ex, "Overflow error aggregating Ingredient '{IngredientName}' for ProductId {ProductId}, Order {OrderId}. Ingredient totals may be incorrect.",
                    ingredientInfo.IngredientName, item.ProductId, orderId);
            }
        }

        private static void AddIngredientToTotals(
            IngredientInfo ingredientInfo, 
            int quantity, 
            Dictionary<string, decimal> ingredientTotals)
        {
            decimal requiredAmount = checked(ingredientInfo.Amount * quantity);

            if (ingredientTotals.TryGetValue(ingredientInfo.IngredientName, out decimal currentTotal))
            {
                ingredientTotals[ingredientInfo.IngredientName] = checked(currentTotal + requiredAmount);
            }
            else
            {
                ingredientTotals.Add(ingredientInfo.IngredientName, requiredAmount);
            }
        }
    }
} 