using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    /// <summary>
    /// Responsible for reading order files, parsing line items using streaming,
    /// handling granular parsing errors, and buffering valid line items.
    /// </summary>
    public interface IOrderBatchProcessor
    {
        /// <summary>
        /// Reads order line items from multiple files using streaming, skips invalid lines,
        /// and buffers valid lines grouped by OrderId.
        /// </summary>
        /// <param name="filePaths">Paths to the order files to process.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing the buffer dictionary mapping OrderId to a list of successfully parsed raw line items with their source file information,
        /// and the count of skipped line items.</returns>
        Task<(Dictionary<string, List<BufferedLineItemInfo>> Buffer, long SkippedCount)> BufferOrderLinesAsync(
            IEnumerable<string> filePaths,
            CancellationToken cancellationToken = default);
    }
} 