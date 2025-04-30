namespace PizzeriaOrderProcessor.Domain
{
    /// <summary>
    /// Represents a single line item as read directly from the input JSON file,
    /// before validation and aggregation.
    /// </summary>
    public class RawOrderLineItem
    {
        public string OrderId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime DeliverAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CustomerAddress { get; set; } = string.Empty;
    }
}
