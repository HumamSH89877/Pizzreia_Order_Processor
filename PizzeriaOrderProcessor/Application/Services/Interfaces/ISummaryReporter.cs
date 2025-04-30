using ValidationResult = FluentValidation.Results.ValidationResult;
using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    /// <summary>
    /// Responsible for displaying the final processing summary.
    /// </summary>
    public interface ISummaryReporter
    {
        /// <summary>
        /// Displays the processing summary to the console using Spectre.Console.
        /// </summary>
        /// <param name="processedOrders">Collection of successfully processed orders attempted for queueing.</param>
        /// <param name="invalidOrderResults">Dictionary mapping OrderIds to their validation results for orders that failed validation.</param>
        /// <param name="ingredientTotals">Dictionary mapping ingredient names to their total required amount across processed orders.</param>
        /// <param name="skippedLineItemCount">Count of line items skipped during initial file parsing.</param>
        /// <param name="invalidOrderSourceFiles">Optional dictionary mapping invalid OrderIds to their source file names.</param>
        /// <param name="validOrderSourceFiles">Optional dictionary mapping valid OrderIds to their source file names.</param>
        void DisplaySummary(
            IEnumerable<Order> processedOrders,
            IReadOnlyDictionary<string, ValidationResult> invalidOrderResults,
            IReadOnlyDictionary<string, decimal> ingredientTotals,
            long skippedLineItemCount,
            IReadOnlyDictionary<string, string>? invalidOrderSourceFiles = null,
            IReadOnlyDictionary<string, string>? validOrderSourceFiles = null);
    }
}