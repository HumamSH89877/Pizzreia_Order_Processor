using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    /// <summary>
    /// Handles batching and pushing orders to the queue,
    /// managing transient errors per sub-batch.
    /// </summary>
    public interface IBatchQueuePusher
    {
        /// <summary>
        /// Breaks down orders into sub-batches based on configuration and attempts to push each to the queue.
        /// Logs errors for failed sub-batches but continues with the rest.
        /// </summary>
        /// <param name="validOrders">The collection of orders to push.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task PushOrdersToQueueAsync(IEnumerable<Order> validOrders, CancellationToken cancellationToken = default);
    }
} 