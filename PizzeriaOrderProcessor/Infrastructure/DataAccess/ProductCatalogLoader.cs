using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;
using PizzeriaOrderProcessor.Application.Services.Interfaces;

namespace PizzeriaOrderProcessor.Infrastructure.DataAccess
{
    public class ProductCatalogLoader : IProductCatalogLoader
    {
        private readonly ILogger<ProductCatalogLoader> _logger;
        private readonly decimal _defaultVAT;
        private readonly string _catalogFilePath; 
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public ProductCatalogLoader(
            IOptions<VatSettings> vatOptions,
            IOptions<FileSettings> fileOptions,
            IHostEnvironment environment,
            ILogger<ProductCatalogLoader> logger)
        {
            _logger = logger;

           
            _defaultVAT = vatOptions.Value.DefaultFallbackValue;
            if (_defaultVAT < 0 || _defaultVAT > 1)
            {
                _logger.LogWarning("Configured DefaultVATFallbackValue ({DefaultVat}) is outside the valid range [0, 1]. Using 0.", _defaultVAT);
                _defaultVAT = 0;
            }

            
            var relativePath = fileOptions.Value.ProductFilePath;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                _logger.LogCritical("ProductFilePath is not configured in FileSettings.");
                throw new InvalidOperationException("ProductFilePath configuration is missing.");
            }
            _catalogFilePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, relativePath));
            _logger.LogInformation("Resolved product catalog path to: {FullPath}", _catalogFilePath);

             _jsonSerializerOptions = new JsonSerializerOptions
             {
                 PropertyNameCaseInsensitive = true
             };
        }

        public FrozenDictionary<string, Product> LoadCatalog()
        {
            ValidateProductCatalogFileExists();
            
            string json = ReadProductCatalogFile();
            List<Product> products = DeserializeProducts(json);
            
            ValidateProductIds(products);
            ValidateAndNormalizeProductData(products);
            
            return CreateFrozenProductCatalog(products);
        }

        private void ValidateProductCatalogFileExists()
        {
            if (!File.Exists(_catalogFilePath))
            {
                _logger.LogCritical("Product catalog file not found: {Path}. Application cannot proceed without product data.", _catalogFilePath);
                throw new FileNotFoundException($"Product catalog file not found: {_catalogFilePath}", _catalogFilePath);
            }
        }

        private string ReadProductCatalogFile()
        {
            try
            {
                return File.ReadAllText(_catalogFilePath);
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex, "Error reading product catalog file: {Path}", _catalogFilePath);
                throw new IOException($"Failed to read product catalog file: {_catalogFilePath}", ex);
            }
        }

        private List<Product> DeserializeProducts(string json)
        {
            try
            {
                var products = JsonSerializer.Deserialize<List<Product>>(json, _jsonSerializerOptions) ?? new List<Product>();
                _logger.LogInformation("Successfully deserialized {ProductCount} products from {Path}", products.Count, _catalogFilePath);
                return products;
            }
            catch (JsonException ex)
            {
                _logger.LogCritical(ex, "Failed to parse product catalog JSON file: {Path}. Check JSON structure and data types.", _catalogFilePath);
                throw new InvalidDataException($"Failed to parse product catalog JSON from {_catalogFilePath}", ex);
            }
        }

        private void ValidateProductIds(List<Product> products)
        {
            var duplicateIds = products.GroupBy(p => p.ProductId)
                                     .Where(g => g.Count() > 1)
                                     .Select(g => g.Key)
                                     .ToList();
            if (duplicateIds.Any())
            {
                 var duplicateIdList = string.Join(", ", duplicateIds);
                 _logger.LogCritical("Duplicate ProductId(s) found in {Path}: {DuplicateIds}", _catalogFilePath, duplicateIdList);
                throw new InvalidDataException($"Duplicate ProductId(s) found in {_catalogFilePath}: {duplicateIdList}");
            }
        }

        private void ValidateAndNormalizeProductData(List<Product> products)
        {
            foreach (var product in products)
            {
                ValidateProductId(product);
                NormalizeProductVat(product);
                ValidateProductPrice(product);
            }
        }

        private void ValidateProductId(Product product)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(product.ProductId ?? "", @"^PRD-\d{5}$"))
            {
                _logger.LogWarning("Invalid ProductId format found: '{ProductId}' in {Path}. Expected 'PRD-NNNNN'.", product.ProductId, _catalogFilePath);
                throw new InvalidDataException($"Invalid ProductId format '{product.ProductId}' found in {_catalogFilePath}.");
            }
        }

        private void NormalizeProductVat(Product product)
        {
            if (product.VAT.HasValue)
            {
                if (product.VAT.Value < 0 || product.VAT.Value > 1)
                {
                    _logger.LogWarning("Invalid VAT multiplier ({VatValue}) found for ProductId {ProductId} in {Path}. Applying default fallback value ({DefaultVat}).",
                        product.VAT.Value, product.ProductId, _catalogFilePath, _defaultVAT);
                    product.VAT = _defaultVAT;
                }
                // else: valid VAT provided, keep it.
            }
            else
            {
                _logger.LogDebug("VAT multiplier not provided for ProductId {ProductId} in {Path}. Applying default fallback value ({DefaultVat}).",
                    product.ProductId, _catalogFilePath, _defaultVAT);
                product.VAT = _defaultVAT; 
            }
        }

        private void ValidateProductPrice(Product product)
        {
            if (product.UnitPriceExclVat < 0)
            {
                _logger.LogWarning("Negative UnitPriceExclVat ({Price}) found for ProductId {ProductId} in {Path}. Treating as invalid data.",
                    product.UnitPriceExclVat, product.ProductId, _catalogFilePath);
                throw new InvalidDataException($"Negative UnitPriceExclVat ({product.UnitPriceExclVat}) found for ProductId {product.ProductId} in {_catalogFilePath}.");
            }
        }

        private FrozenDictionary<string, Product> CreateFrozenProductCatalog(List<Product> products)
        {
            try
            {
                var frozenCatalog = products.ToFrozenDictionary(p => p.ProductId);
                _logger.LogInformation("Product catalog loaded and frozen successfully from {Path}. Contains {Count} unique products.", _catalogFilePath, frozenCatalog.Count);
                return frozenCatalog;
            }
            catch (ArgumentException ex) // Handles potential issues during ToFrozenDictionary if duplicates somehow slipped through
            {
                _logger.LogCritical(ex, "Error creating frozen dictionary for product catalog from {Path}.", _catalogFilePath);
                throw new InvalidOperationException($"Failed to create frozen dictionary for product catalog from {_catalogFilePath}", ex);
            }
        }
    }
}
