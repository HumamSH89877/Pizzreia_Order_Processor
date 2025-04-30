using System.Collections.Frozen;
using PizzeriaOrderProcessor.Configuration;
using PizzeriaOrderProcessor.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzeriaOrderProcessor.Application.Services.Interfaces;

namespace PizzeriaOrderProcessor.Application.Services.Implementations
{
    public class OrderCalculator : IOrderCalculator
    {
        private readonly FrozenDictionary<string, Product> _productCatalog;
        private readonly FrozenDictionary<string, List<IngredientInfo>> _ingredientMappings;
        private readonly decimal _vatFallback;
        private readonly ILogger<OrderCalculator> _logger;

        public OrderCalculator(FrozenDictionary<string, Product> productCatalog,
                               FrozenDictionary<string, List<IngredientInfo>> ingredientMappings,
                               IOptions<VatSettings> vatOptions,
                               ILogger<OrderCalculator> logger)
        {
            _productCatalog = productCatalog ?? throw new ArgumentNullException(nameof(productCatalog));
            _ingredientMappings = ingredientMappings ?? throw new ArgumentNullException(nameof(ingredientMappings));
            _vatFallback = vatOptions?.Value?.DefaultFallbackValue
                ?? throw new ArgumentNullException(nameof(vatOptions), "VatSettings configuration is missing or DefaultFallbackValue is not set.");
            _logger = logger;
        }

        public decimal CalculateOrderTotal(Order order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            decimal total = 0m;
            foreach (var item in order.Items)
            {
                if (!_productCatalog.TryGetValue(item.ProductId, out var product))
                {
                    _logger.LogWarning("ProductId {ProductId} not found in catalog. Skipping item.", item.ProductId);
                    continue; 
                }

                decimal vat = product.VAT ?? _vatFallback;
                decimal lineTotal = product.UnitPriceExclVat * (1 + vat) * item.Quantity;
                total += lineTotal;
            }
            return total;
        }

        public Dictionary<string, decimal> CalculateTotalIngredients(IEnumerable<Order> orders)
        {
            if (orders == null) throw new ArgumentNullException(nameof(orders));
            var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var order in orders)
            {
                foreach (var item in order.Items)
                {
                    if (!_ingredientMappings.TryGetValue(item.ProductId, out var ingredients))
                    {
                        _logger.LogWarning("ProductId {ProductId} not found in ingredient mappings. Skipping item.", item.ProductId);
                        continue; 
                    }

                    foreach (var info in ingredients)
                    {
                        decimal required = info.Amount * item.Quantity;
                        if (totals.ContainsKey(info.IngredientName))
                            totals[info.IngredientName] += required;
                        else
                            totals[info.IngredientName] = required;
                    }
                }
            }

            return totals;
        }

        public void CalculateOrderTotals(Order order)
        {
            // Precondition checks (should be guaranteed by orchestrator, but defensive)
            if (order == null) { _logger.LogWarning("CalculateOrderTotals called with null order."); return; }
            if (order.Items == null) { _logger.LogWarning("CalculateOrderTotals called for Order {OrderId} with null Items list.", order.OrderId); order.TotalAmountIncludingVat = 0; return; }

            // Reset totals
            decimal orderTotal = 0m;

            foreach (var item in order.Items)
            {
                if (item == null) { _logger.LogWarning("CalculateOrderTotals encountered null item in Order {OrderId}.", order.OrderId); continue; }

                // Check critical linked data
                if (item.AssociatedProduct == null)
                {
                    _logger.LogWarning("CalculateOrderTotals encountered OrderItem with null AssociatedProduct for ProductId {ProductId} in Order {OrderId}. Cannot calculate totals accurately.", item.ProductId, order.OrderId);
                    continue; 
                }

                // Calculate ItemTotalInclVat on the fly
                decimal vat = item.AssociatedProduct.VAT ?? _vatFallback;
                decimal itemTotalInclVat = item.AssociatedProduct.UnitPriceExclVat * (1 + vat) * item.Quantity;

                try // Add item total to order total, catch overflow
                {
                    // Use the calculated value
                    orderTotal = checked(orderTotal + itemTotalInclVat);
                }
                catch (OverflowException ex) when (order != null)
                {
                    _logger.LogError("Overflow detected while calculating total for Order {OrderId}. Capping at MaxValue. Error: {ErrorMessage}", order.OrderId, ex.Message);
                    _logger.LogDebug(ex, "Detailed OverflowException for Order {OrderId}", order.OrderId);
                    order.TotalAmountIncludingVat = decimal.MaxValue; // Cap at max value
                    return; // Stop calculation for this order
                }
            }

            order.TotalAmountIncludingVat = orderTotal;
        }
    }
}
