using System.ComponentModel.DataAnnotations;

namespace PizzeriaOrderProcessor.Configuration
{
    public class TimeSettings
    {
        public const string SectionName = "TimeSettings";
        
        [Required]
        public int PrepBufferBaseMinutes { get; set; } = 30;
        
        [Required]
        public int PrepBufferPerItemMinutes { get; set; } = 5;
        
        [Required]
        public int MaximumAdvanceOrderHours { get; set; } = 168; // 1 week
    }
}
