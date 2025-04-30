using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    /// <summary>
    /// Interface for a mock queue client.
    /// </summary>
    public interface IMockQueue
    {
        /// <summary>
        /// Simulates pushing a batch of orders asynchronously.
        /// May throw exceptions to simulate transient failures.
        /// </summary>
        /// <param name="orders">The batch of orders to push.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown to simulate transient queue failures.</exception>
        Task PushBatchAsync(IEnumerable<Order> orders);
    }
}
