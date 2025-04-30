using System.ComponentModel.DataAnnotations;

namespace PizzeriaOrderProcessor.Configuration
{
    public class OperatingHoursSettings
    {
        public const string SectionName = "OperatingHours";
        
        [Required]
        [RegularExpression(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$", ErrorMessage = "Time must be in format HH:MM:SS")]
        public string Open { get; set; } = "08:00:00"; // Default if missing
        
        [Required]
        [RegularExpression(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$", ErrorMessage = "Time must be in format HH:MM:SS")]
        public string Close { get; set; } = "22:00:00"; 
    }
}
