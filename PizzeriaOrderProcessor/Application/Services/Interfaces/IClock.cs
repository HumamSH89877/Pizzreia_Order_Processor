namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    /// <summary>
    /// Provides an abstraction for the current time, primarily for testability.
    /// </summary>
    public interface IClock
    {
        /// <summary>
        /// Gets the current UTC date and time.
        /// </summary>
        DateTimeOffset UtcNow { get; }
    }
}
