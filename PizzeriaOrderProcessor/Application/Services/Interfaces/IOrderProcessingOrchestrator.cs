namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    /// <summary>
    /// Orchestrates the entire order batch processing workflow.
    /// </summary>
    public interface IOrderProcessingOrchestrator
    {
        /// <summary>
        /// Processes a batch of orders from the given input files.
        /// Reads files, validates orders, calculates totals, aggregates ingredients,
        /// attempts to push valid orders to a queue, and displays a summary report.
        /// </summary>
        /// <param name="inputFilePaths">An enumerable collection of paths to the order files.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A Task representing the asynchronous processing operation.</returns>
        Task ProcessOrderBatchAsync(IEnumerable<string> inputFilePaths, CancellationToken cancellationToken);
    }
} 