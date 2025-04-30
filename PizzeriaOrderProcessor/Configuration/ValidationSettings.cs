using System.ComponentModel.DataAnnotations;

namespace PizzeriaOrderProcessor.Configuration
{
    public class ValidationSettings
    {
        public const string SectionName = "Validation";
        
        [Required]
        public decimal MinimumOrderAmount { get; set; } = 10.0m; 
        
        [Required]
        public int ItemQuantityUpperLimit { get; set; } = 100;
        
        public string AddressRegex { get; set; } = @"^(\d{1,}) [a-zA-Z0-9\s]+(,)? [a-zA-Z]+(,)? [A-Z]{2} [0-9]{5,6}$";
    }
}
