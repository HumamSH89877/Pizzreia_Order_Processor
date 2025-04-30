using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;
using PizzeriaOrderProcessor.Application.Services.Interfaces;

namespace PizzeriaOrderProcessor.Application.Services.Implementations
{
    public class BatchQueuePusher : IBatchQueuePusher
    {
        private readonly IMockQueue _mockQueue;
        private readonly int _batchSize;
        private readonly ILogger<BatchQueuePusher> _logger;

        public BatchQueuePusher(
            IMockQueue mockQueue,
            IOptions<QueueSettings> queueOptions,
            ILogger<BatchQueuePusher> logger)
        {
            _mockQueue = mockQueue;
            _batchSize = queueOptions.Value.BatchSize > 0 ? queueOptions.Value.BatchSize : 50; 
            _logger = logger;
            _logger.LogInformation("BatchQueuePusher initialized with push batch size {BatchSize}.", _batchSize);
        }

        public async Task PushOrdersToQueueAsync(IEnumerable<Order> validOrders, CancellationToken cancellationToken = default)
        {
            var allOrdersList = validOrders?.ToList() ?? new List<Order>();
            int totalOrderCount = allOrdersList.Count;

            if (totalOrderCount == 0)
            {
                _logger.LogInformation("No valid orders to push to the queue.");
                return;
            }

             _logger.LogInformation("Starting to push {TotalOrderCount} valid orders to queue in batches of {BatchSize}.", totalOrderCount, _batchSize);

            int batchNumber = 0;
            int pushedCount = 0;
            int failedBatchCount = 0;

            
            foreach (var subBatch in allOrdersList.Chunk(_batchSize))
            {
                batchNumber++;
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Queue push operation cancelled mid-process.");
                    break;
                }

                var subBatchList = subBatch.ToList(); 
                string firstId = subBatchList.FirstOrDefault()?.OrderId ?? "N/A";
                string lastId = subBatchList.LastOrDefault()?.OrderId ?? "N/A";
                int subBatchCount = subBatchList.Count;

                _logger.LogInformation("Attempting to push sub-batch {BatchNumber}/{TotalBatches} ({SubBatchCount} orders: {FirstId} to {LastId})...",
                    batchNumber, (int)Math.Ceiling((double)totalOrderCount / _batchSize), subBatchCount, firstId, lastId);

                try
                {
                    await _mockQueue.PushBatchAsync(subBatchList).ConfigureAwait(false);
                    pushedCount += subBatchCount;
                    _logger.LogInformation("Successfully pushed sub-batch {BatchNumber} ({FirstId} to {LastId}). Total pushed so far: {PushedCount}", batchNumber, firstId, lastId, pushedCount);
                }
                catch (Exception ex)
                {
                    failedBatchCount++;
                    _logger.LogError("Failed to push sub-batch {BatchNumber} ({SubBatchCount} orders: {FirstId} to {LastId}) to queue. Error: {ErrorMessage}[/]",
                        batchNumber, subBatchCount, firstId, lastId, ex.Message);
                    _logger.LogDebug(ex, "Detailed exception for sub-batch {BatchNumber} ({FirstId} to {LastId})",
                        batchNumber, firstId, lastId);

                    // Mark all orders in this failed sub-batch as failed
                    foreach (var failedOrder in subBatch)
                    {
                        failedOrder.QueuePushSucceeded = false;
                        _logger.LogDebug("Marked order {OrderId} as failed to push to queue", failedOrder.OrderId);
                    }
                }
            }

            _logger.LogInformation("Finished attempting to push all orders. Total pushed: {PushedCount}/{TotalOrderCount}. Failed sub-batches: {FailedBatchCount}.", pushedCount, totalOrderCount, failedBatchCount);
        }
    }
}
