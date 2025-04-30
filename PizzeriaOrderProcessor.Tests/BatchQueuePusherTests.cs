using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;
using PizzeriaOrderProcessor.Application.Services.Interfaces;
using PizzeriaOrderProcessor.Application.Services.Implementations;

namespace PizzeriaOrderProcessor.Tests
{
    public class BatchQueuePusherTests
    {
        private readonly Mock<IMockQueue> _mockQueue;
        private readonly IOptions<QueueSettings> _queueSettings;
        private readonly BatchQueuePusher _pusher;

        public BatchQueuePusherTests()
        {
            _mockQueue = new Mock<IMockQueue>();
            _queueSettings = Microsoft.Extensions.Options.Options.Create(new QueueSettings { BatchSize = 2 });
            _pusher = new BatchQueuePusher(_mockQueue.Object, _queueSettings, NullLogger<BatchQueuePusher>.Instance);
        }

        // Helper to create Order objects
        private Order CreateTestOrder(string orderId, string productId = "PRD-10001", int quantity = 1)
        {
            return new Order
            {
                OrderId = orderId,
                CustomerAddress = $"Customer {orderId}",
                Items = new List<OrderItem> { new OrderItem { ProductId = productId, Quantity = quantity } },
                CreatedAt = DateTimeOffset.UtcNow.UtcDateTime,
                DeliverAt = DateTimeOffset.UtcNow.AddHours(1).UtcDateTime
            };
        }

        [Fact]
        public async Task PushOrdersToQueueAsync_PushesInBatches()
        {
            // Arrange
            var orders = new List<Order>
            {
                CreateTestOrder("ORD-00001"),
                CreateTestOrder("ORD-00002"),
                CreateTestOrder("ORD-00003")
            };

            // Act
            await _pusher.PushOrdersToQueueAsync(orders, CancellationToken.None);

            // Assert
            _mockQueue.Verify(q => q.PushBatchAsync(It.Is<IEnumerable<Order>>(batch => batch.Count() == 2)), Times.Once());
            _mockQueue.Verify(q => q.PushBatchAsync(It.Is<IEnumerable<Order>>(batch => batch.Count() == 1)), Times.Once());
            _mockQueue.Verify(q => q.PushBatchAsync(It.IsAny<IEnumerable<Order>>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PushOrdersToQueueAsync_HandlesEmptyList()
        {
            // Arrange
            var orders = new List<Order>(); // Empty list of Order

            // Act
            await _pusher.PushOrdersToQueueAsync(orders, CancellationToken.None);

            // Assert
            _mockQueue.Verify(q => q.PushBatchAsync(It.IsAny<IEnumerable<Order>>()), Times.Never());
        }
    }
}
