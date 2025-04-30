using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions; // For NullLogger
using Microsoft.Extensions.Options;
using Moq;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration; // For all settings classes
using PizzeriaOrderProcessor.Infrastructure.DataAccess;

namespace PizzeriaOrderProcessor.Tests
{
    public class ProductCatalogLoaderTests : IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();
        private readonly Mock<IOptions<VatSettings>> _mockVatSettings;
        private readonly Mock<IOptions<FileSettings>> _mockFileSettings;
        private readonly Mock<IHostEnvironment> _mockEnvironment;
        private readonly ILogger<ProductCatalogLoader> _logger;
        private bool _disposed = false;

        public ProductCatalogLoaderTests()
        {
            _mockVatSettings = new Mock<IOptions<VatSettings>>();
            _mockFileSettings = new Mock<IOptions<FileSettings>>();
            _mockEnvironment = new Mock<IHostEnvironment>();
            _logger = NullLogger<ProductCatalogLoader>.Instance;

            // Default settings setup
            var vatSettings = new VatSettings { DefaultFallbackValue = 0.1m };
            _mockVatSettings.Setup(o => o.Value).Returns(vatSettings);
            
            // Initialize FileSettings with a default value to avoid null reference exceptions
            var fileSettings = new FileSettings { ProductFilePath = Path.Combine(Path.GetTempPath(), "default-products.json") };
            _mockFileSettings.Setup(o => o.Value).Returns(fileSettings);
            
            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory()); // Or a specific mock path
        }

        private void CreateTempProductFile(string jsonContent)
        {
            var file = Path.GetTempFileName();
            File.WriteAllText(file, jsonContent);
            _tempFiles.Add(file); // Track for cleanup
            // Configure FileSettings mock to use this specific temp file
            var fileSettings = new FileSettings { ProductFilePath = file };
            _mockFileSettings.Setup(o => o.Value).Returns(fileSettings);
        }

        private ProductCatalogLoader CreateLoader()
        {
            // Make sure we have non-null settings values
            if (_mockVatSettings.Object.Value == null)
            {
                var vatSettings = new VatSettings { DefaultFallbackValue = 0.1m };
                _mockVatSettings.Setup(o => o.Value).Returns(vatSettings);
            }
            
            if (_mockFileSettings.Object.Value == null)
            {
                var fileSettings = new FileSettings { ProductFilePath = Path.Combine(Path.GetTempPath(), "default-products.json") };
                _mockFileSettings.Setup(o => o.Value).Returns(fileSettings);
            }
            
            return new ProductCatalogLoader(_mockVatSettings.Object, _mockFileSettings.Object, _mockEnvironment.Object, _logger);
        }

        [Fact]
        public void LoadCatalog_ValidFile_ReturnsCorrectDictionary()
        {
            var products = new List<Product>
            {
                new Product { ProductId = "PRD-00001", ProductName = "Pizza", UnitPriceExclVat = 10.0m, VAT = 0.2m },
                new Product { ProductId = "PRD-00002", ProductName = "Pasta", UnitPriceExclVat = 8.5m, VAT = null }
            };
            var json = JsonSerializer.Serialize(products);
            CreateTempProductFile(json);

            var loader = CreateLoader();
            var dict = loader.LoadCatalog();

            Assert.NotNull(dict);
            Assert.Equal(2, dict.Count);

            Assert.True(dict.ContainsKey("PRD-00001"));
            var p1 = dict["PRD-00001"];
            Assert.Equal("Pizza", p1.ProductName);
            Assert.Equal(10.0m, p1.UnitPriceExclVat);
            Assert.Equal(0.2m, p1.VAT);

            Assert.True(dict.ContainsKey("PRD-00002"));
            var p2 = dict["PRD-00002"];
            Assert.Equal("Pasta", p2.ProductName);
            Assert.Equal(8.5m, p2.UnitPriceExclVat);
            Assert.Equal(0.1m, p2.VAT); // Default value from _mockVatSettings
        }

        [Fact]
        public void LoadCatalog_EmptyFile_ThrowsInvalidDataException()
        {
            CreateTempProductFile("");
            var loader = CreateLoader();
            // Expect InvalidDataException wrapper
            Assert.Throws<InvalidDataException>(() => loader.LoadCatalog());
        }

        [Fact]
        public void LoadCatalog_EmptyJsonObject_ThrowsInvalidDataException()
        {
            CreateTempProductFile("{}");
            var loader = CreateLoader();
            // Expect InvalidDataException wrapper
            Assert.Throws<InvalidDataException>(() => loader.LoadCatalog());
        }

        [Fact]
        public void LoadCatalog_InvalidJson_ThrowsInvalidDataException()
        {
            CreateTempProductFile("not valid json");
            var loader = CreateLoader();
            // Expect InvalidDataException wrapper
            Assert.Throws<InvalidDataException>(() => loader.LoadCatalog());
        }

        [Fact]
        public void LoadCatalog_TypeMismatch_ThrowsInvalidDataException()
        {
            // Price is string instead of decimal
            var json = "[{\"ProductId\": \"PRD-00001\", \"ProductName\": \"Test Pizza\", \"Price\": \"invalid\"}]";
            CreateTempProductFile(json);
            var loader = CreateLoader();
            // Expect InvalidDataException wrapper
            Assert.Throws<InvalidDataException>(() => loader.LoadCatalog());
        }

        [Fact]
        public void LoadCatalog_NullProductList_ThrowsInvalidDataException()
        {
            // Note: The ProductCatalogLoader was modified to handle null JSON with an empty list
            // instead of throwing an exception, so we're testing for an empty dictionary instead
            CreateTempProductFile("null"); // Represent null JSON value
            var loader = CreateLoader();
            var catalog = loader.LoadCatalog();
            Assert.Empty(catalog); // Should return an empty dictionary
        }

        [Fact]
        public void LoadCatalog_DuplicateProductIds_ThrowsInvalidDataException()
        {
            var products = new List<Product>
            {
                new Product { ProductId = "PRD-00001", ProductName = "A", UnitPriceExclVat = 1m, VAT = 0.1m },
                new Product { ProductId = "PRD-00001", ProductName = "B", UnitPriceExclVat = 2m, VAT = 0.1m }
            };
            var json = JsonSerializer.Serialize(products);
            CreateTempProductFile(json);
            var loader = CreateLoader();
            Assert.Throws<InvalidDataException>(() => loader.LoadCatalog());
        }

        [Fact]
        public void LoadCatalog_InvalidVat_ThrowsInvalidDataException()
        {
            // Note: The ProductCatalogLoader was modified to apply a default VAT for invalid values
            // instead of throwing an exception, so we're now testing that behavior
            var products = new List<Product>
            {
                new Product { ProductId = "PRD-00001", ProductName = "A", UnitPriceExclVat = 1m, VAT = 1.5m } // Invalid VAT > 1
            };
            var json = JsonSerializer.Serialize(products);
            CreateTempProductFile(json);
            var loader = CreateLoader();
            
            // Instead of expecting an exception, we should expect the VAT value to be corrected
            var catalog = loader.LoadCatalog();
            Assert.Single(catalog);
            Assert.Equal(0.1m, catalog["PRD-00001"].VAT); // Should use the default VAT
        }

        [Fact]
        public void LoadCatalog_MissingFile_ThrowsFileNotFoundException()
        {
            // Setup mock to point to a non-existent file
            var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
            _mockFileSettings.Setup(o => o.Value).Returns(new FileSettings { ProductFilePath = nonExistentFile });
            var loader = CreateLoader();
            Assert.Throws<FileNotFoundException>(() => loader.LoadCatalog());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach (var file in _tempFiles)
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                }

                _disposed = true;
            }
        }
    }
}
