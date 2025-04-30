using System.Text.Json;
using Microsoft.Extensions.Logging;
using PizzeriaOrderProcessor.Application.Services.Interfaces;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Logging;
using PizzeriaOrderProcessor.Validators;

namespace PizzeriaOrderProcessor.Application.Services.Implementations
{
    public class OrderBatchProcessor : IOrderBatchProcessor
    {
        private readonly ILogger<OrderBatchProcessor> _logger;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public OrderBatchProcessor(ILogger<OrderBatchProcessor> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<(Dictionary<string, List<BufferedLineItemInfo>> Buffer, long SkippedCount)> BufferOrderLinesAsync(
            IEnumerable<string> filePaths,
            CancellationToken cancellationToken = default)
        {
            var orderLineBuffer = new Dictionary<string, List<BufferedLineItemInfo>>();
            long skippedLineItemCount = 0;
            var fileList = filePaths?.ToList() ?? new List<string>();

            _logger.LogInformation(LogConstants.OrderFileBufferingStarted, fileList.Count);

            foreach (var filePath in fileList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(LogConstants.BufferingProcessCancelled);
                    break;
                }

                long currentFileSkippedCount = await ProcessSingleFileAsync(
                    filePath, 
                    orderLineBuffer, 
                    cancellationToken);
                
                Interlocked.Add(ref skippedLineItemCount, currentFileSkippedCount);
            }

            _logger.LogInformation(LogConstants.OrderFileBufferingCompleted, 
                orderLineBuffer.Count, skippedLineItemCount);

            return (orderLineBuffer, skippedLineItemCount);
        }

        private async Task<long> ProcessSingleFileAsync(
            string filePath,
            Dictionary<string, List<BufferedLineItemInfo>> orderLineBuffer,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(LogConstants.FileProcessingStarted, filePath);
            long currentFileSkippedCount = 0;
            bool fileHadFatalJsonError = false;

            try
            {
                await using var stream = File.OpenRead(filePath);
                
                await foreach (var rawLineItem in JsonSerializer.DeserializeAsyncEnumerable<RawOrderLineItem>(
                                   stream, _jsonSerializerOptions, cancellationToken).ConfigureAwait(false))
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    TryProcessOrderLineItem(rawLineItem, filePath, orderLineBuffer, ref currentFileSkippedCount);
                }
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, LogConstants.FileNotFound, filePath);
                // Continue to the next file for resilience
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, LogConstants.IoError, filePath);
                _logger.LogDebug(ex, "Detailed IOException for file {FilePath}", filePath); 
            }
            catch (JsonException ex)
            {
                // This catches errors if the overall JSON structure is invalid
                // OR if an error occurs *during* streaming enumeration itself (e.g., bad token).
                _logger.LogError(ex, LogConstants.MalformedJson, filePath);
                _logger.LogDebug(ex, "Detailed JSON parsing exception in file {FilePath}", filePath); 
                // We count this as at least one skip, could be more if items were already read
                if (currentFileSkippedCount == 0) currentFileSkippedCount++;
                // Continue to the next file
            }
            catch (Exception ex) // Catch-all for unexpected errors per file
            {
                _logger.LogError(ex, LogConstants.UnexpectedError, filePath);
                _logger.LogDebug(ex, "Detailed unexpected exception for file {FilePath}", filePath);                 
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(LogConstants.FileProcessingCompleted, 
                    filePath, currentFileSkippedCount, fileHadFatalJsonError);
            }

            return currentFileSkippedCount;
        }

        private void TryProcessOrderLineItem(
            RawOrderLineItem? rawLineItem, 
            string filePath, 
            Dictionary<string, List<BufferedLineItemInfo>> orderLineBuffer, 
            ref long currentFileSkippedCount)
        {
            // Check item validity *after* successful deserialization attempt
            if (rawLineItem == null)
            {
                _logger.LogWarning(LogConstants.NullLineItem, filePath);
                currentFileSkippedCount++;
                return;
            }

            // Basic validation: OrderId is required for buffering
            if (!IsValidOrderId(rawLineItem.OrderId, rawLineItem, filePath, ref currentFileSkippedCount))
            {
                return;
            }

            // Check ProductId format
            if (!IsValidProductId(rawLineItem.ProductId, rawLineItem, filePath, ref currentFileSkippedCount))
            {
                return;
            }

            // Create a BufferedLineItemInfo that includes both the line item and its source file
            // store one copy of each unique filename in memory
            var fileName = string.Intern(Path.GetFileName(filePath));
            var bufferedInfo = new BufferedLineItemInfo(rawLineItem, fileName);
            
            // Add the valid line item to the buffer
            AddLineItemToBuffer(rawLineItem.OrderId, bufferedInfo, orderLineBuffer);
        }

        private bool IsValidOrderId(
            string? orderId, 
            RawOrderLineItem rawLineItem, 
            string filePath, 
            ref long currentFileSkippedCount)
        {
            if (string.IsNullOrWhiteSpace(orderId) ||
                !System.Text.RegularExpressions.Regex.IsMatch(orderId, ValidationConstants.OrderIdPattern))
            {
                _logger.LogWarning(LogConstants.OrderIdInvalid, 
                    orderId, filePath, rawLineItem);
                currentFileSkippedCount++;
                return false;
            }
            
            return true;
        }

        private bool IsValidProductId(
            string? productId, 
            RawOrderLineItem rawLineItem, 
            string filePath, 
            ref long currentFileSkippedCount)
        {
            if (string.IsNullOrWhiteSpace(productId) ||
                !System.Text.RegularExpressions.Regex.IsMatch(productId, ValidationConstants.ProductIdPattern))
            {
                _logger.LogWarning(LogConstants.ProductIdInvalid, 
                    productId, filePath, rawLineItem);
                currentFileSkippedCount++;
                return false;
            }
            
            return true;
        }

        private static void AddLineItemToBuffer(
            string orderId, 
            BufferedLineItemInfo bufferedInfo, 
            Dictionary<string, List<BufferedLineItemInfo>> orderLineBuffer)
        {
            if (!orderLineBuffer.TryGetValue(orderId, out var items))
            {
                items = new List<BufferedLineItemInfo>();
                orderLineBuffer.Add(orderId, items);
            }
            items.Add(bufferedInfo);
        }
    }
} 