namespace PizzeriaOrderProcessor.Domain
{
    /// <summary>
    /// Represents information about a single ingredient required for a product.
    /// </summary>
    public class IngredientInfo
    {
        public string IngredientName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
