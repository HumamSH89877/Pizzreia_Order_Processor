using System.ComponentModel.DataAnnotations;

namespace PizzeriaOrderProcessor.Configuration
{
    public class FileSettings
    {
        // Property names must match the keys in appsettings.json
        public const string SectionName = "FileSettings";
        
        [Required]
        public string ProductFilePath { get; set; } = string.Empty;
        
        [Required]
        public string IngredientFilePath { get; set; } = string.Empty;
    }
}
