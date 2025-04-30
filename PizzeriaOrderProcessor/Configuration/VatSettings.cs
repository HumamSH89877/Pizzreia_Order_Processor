using System.ComponentModel.DataAnnotations;

namespace PizzeriaOrderProcessor.Configuration
{
    public class VatSettings
    {
        public const string SectionName = "VatSettings";
        
        [Required]
        public decimal DefaultFallbackValue { get; set; } = 0.05m;
    }
}
