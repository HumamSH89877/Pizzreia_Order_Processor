namespace PizzeriaOrderProcessor.Domain
{
    /// <summary>
    /// Represents a single item within a reconstructed Order.
    /// </summary>
    public class OrderItem
    {
        public string ProductId { get; set; } = string.Empty; 
        public int Quantity { get; set; }
        // Link to the full product details loaded from the catalog.
        // Should be non-null after successful reconstruction.
        public Product? AssociatedProduct { get; set; }
        // Calculated total price for this item, including VAT.
        public decimal ItemTotalInclVat { get; set; }
    }
} 