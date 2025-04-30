namespace PizzeriaOrderProcessor.Domain
{
    /// <summary>
    /// Represents the mapping of a ProductId to its list of ingredients.
    /// Used for deserializing ingredients.json.
    /// </summary>
    public class IngredientMapping
    {
        public string ProductId { get; set; } = string.Empty; 
        public List<IngredientInfo> Ingredients { get; set; } = new();
    }
}
