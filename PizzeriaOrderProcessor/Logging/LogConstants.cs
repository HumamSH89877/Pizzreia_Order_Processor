namespace PizzeriaOrderProcessor.Logging
{
    /// <summary>
    /// Contains common log message templates to improve consistency and eliminate repetition
    /// </summary>
    public static class LogConstants
    {
        // File processing messages
        public const string FileProcessingStarted = "Processing file: {FilePath}";
        public const string FileProcessingCompleted = "Finished processing file: {FilePath}. Skipped items in this file: {SkippedCount}. Fatal JSON error: {FatalJsonError}";
        public const string FileNotFound = "Order file not found: {FilePath}. Skipping this file.";
        public const string IoError = "IO error reading order file: {FilePath}. Skipping this file.";
        public const string MalformedJson = "Malformed JSON in {FilePath} – skipping file.";
        public const string UnexpectedError = "Unexpected error processing file: {FilePath}. Skipping this file.";
        
        // Buffer processing messages
        public const string OrderFileBufferingStarted = "Starting order file buffering process for {FileCount} file(s).";
        public const string OrderFileBufferingCompleted = "Finished buffering all files. Buffered {OrderCount} unique OrderIds. Total skipped line items across all files: {SkippedCount}";
        public const string BufferingProcessCancelled = "Buffering process cancelled.";
        
        // Validation error messages
        public const string OrderIdInvalid = "Skipping order line item with missing or invalid format OrderId ('{OrderId}') in file: {FilePath}. Data: {@RawLineItem}";
        public const string ProductIdInvalid = "Skipping order line item with missing or invalid format ProductId ('{ProductId}') in file: {FilePath}. Data: {@RawLineItem}";
        public const string NullLineItem = "Skipping null order line item encountered in file: {FilePath}";
    }
}
