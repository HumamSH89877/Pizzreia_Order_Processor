using System.Text.Json.Serialization;

namespace PizzeriaOrderProcessor.Domain
{
    public class Product
    {
        [JsonPropertyName("ProductId")]
        public string ProductId { get; set; } = string.Empty;

        [JsonPropertyName("ProductName")]
        public string ProductName { get; set; } = string.Empty;

        [JsonPropertyName("Price")] 
        public decimal UnitPriceExclVat { get; set; }

        [JsonPropertyName("VAT")] 
        public decimal? VAT { get; set; }
    }
}
