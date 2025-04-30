namespace PizzeriaOrderProcessor.Domain
{
    /// <summary>
    /// Represents a fully reconstructed and validated order, ready for processing or queueing.
    /// </summary>
    public class Order
    {
        public string OrderId { get; set; } = string.Empty; 
        public List<OrderItem> Items { get; set; } = new();
        public DateTime DeliverAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CustomerAddress { get; set; } = string.Empty; 
        public decimal TotalAmountIncludingVat { get; set; } 
        public bool QueuePushSucceeded { get; set; } = true; 
    }
}
