using Microsoft.Extensions.Logging;
using PizzeriaOrderProcessor.Application.Services.Interfaces;
using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Infrastructure.Queue
{
    public class MockQueue : IMockQueue
    {
        private readonly Random _rng;
        private readonly double _failureProbability;
        private readonly ILogger<MockQueue> _logger;

        public MockQueue(ILogger<MockQueue> logger, double failureProbability = 0.2)
        {
            _logger = logger;
            _rng = new Random(); 
            _failureProbability = Math.Clamp(failureProbability, 0.0, 1.0); 
            _logger.LogInformation("MockQueue initialized with {FailureProbability:P0} failure probability.", _failureProbability);
        }

       
        public MockQueue(Random rng, ILogger<MockQueue> logger, double failureProbability = 0.2)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _logger = logger;
            _failureProbability = Math.Clamp(failureProbability, 0.0, 1.0);
            _logger.LogInformation("MockQueue initialized with injected RNG and {FailureProbability:P0} failure probability.", _failureProbability);
        }

        public async Task PushBatchAsync(IEnumerable<Order> orders)
        {
            var orderList = orders?.ToList() ?? new List<Order>();
            int count = orderList.Count;
            if (count == 0)
            {
                _logger.LogWarning("MockQueue.PushBatchAsync called with empty batch.");
                return; // Nothing to push
            }

            string firstId = orderList.FirstOrDefault()?.OrderId ?? "N/A";
            string lastId = orderList.LastOrDefault()?.OrderId ?? "N/A";
            _logger.LogDebug("MockQueue received batch of {Count} orders ({FirstId} to {LastId}). Simulating push...", count, firstId, lastId);

            // Simulate network delay
            int delayMs = _rng.Next(50, 200);
            _logger.LogTrace("Simulating {DelayMs}ms network delay.", delayMs);
            await Task.Delay(delayMs).ConfigureAwait(false); 

            // Simulate transient failure
            if (_rng.NextDouble() < _failureProbability)
            {
                var errorMessage = "Simulated MockQueue transient failure.";
                _logger.LogWarning("Simulating failure for batch of {Count} orders ({FirstId} to {LastId}).", count, firstId, lastId);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogDebug("Successfully simulated push for batch of {Count} orders ({FirstId} to {LastId}).", count, firstId, lastId);
        }
    }
}
