using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    /// <summary>
    /// Calculates totals for individual orders.
    /// </summary>
    public interface IOrderCalculator
    {
        /// <summary>
        /// Calculates the ItemTotalInclVat for each item and the
        /// TotalAmountIncludingVat for the entire order.
        /// Assumes Order.Items and OrderItem.AssociatedProduct are populated and valid.
        /// Modifies the passed-in Order object directly.
        /// </summary>
        /// <param name="order">The Order object to calculate totals for.</param>
        void CalculateOrderTotals(Order order);

        decimal CalculateOrderTotal(Order order);
        Dictionary<string, decimal> CalculateTotalIngredients(IEnumerable<Order> orders);
    }
}
