using System.ComponentModel.DataAnnotations;

namespace PizzeriaOrderProcessor.Configuration
{
    public class QueueSettings
    {
        public const string SectionName = "QueueSettings";
        
        [Required]
        public int BatchSize { get; set; } = 50;
    }
}
