using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    /// <summary>
    /// Aggregates raw ingredient requirements across multiple orders.
    /// </summary>
    public interface IIngredientAggregator
    {
        /// <summary>
        /// Calculates the total amount required for each ingredient across all provided valid orders.
        /// </summary>
        /// <param name="validOrders">An enumerable collection of orders that have passed validation.</param>
        /// <returns>A dictionary mapping ingredient names (case-insensitive) to their total required amount.</returns>
        Dictionary<string, decimal> AggregateIngredients(IEnumerable<Order> validOrders);
    }
} 