using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration; 
using PizzeriaOrderProcessor.Infrastructure.DataAccess;

namespace PizzeriaOrderProcessor.Tests
{
    public class IngredientMappingLoaderTests : IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();
        private readonly Mock<IHostEnvironment> _mockEnvironment = new Mock<IHostEnvironment>();
        private readonly Mock<IOptions<FileSettings>> _mockFileSettings = new Mock<IOptions<FileSettings>>();
        private bool _disposed = false;

        public IngredientMappingLoaderTests()
        {
            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
            
            // Initialize FileSettings with a default value to avoid null reference exceptions
            var fileSettings = new FileSettings { IngredientFilePath = Path.Combine(Path.GetTempPath(), "default-ingredients.json") };
            _mockFileSettings.Setup(o => o.Value).Returns(fileSettings);
        }

        private void CreateTempIngredientFile(string jsonContent)
        {
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
            File.WriteAllText(file, jsonContent);
            _tempFiles.Add(file);
            var fileSettings = new FileSettings { IngredientFilePath = file };
            _mockFileSettings.Setup(o => o.Value).Returns(fileSettings);
        }

        private IngredientMappingLoader CreateLoader()
        {
            // Make sure we have a non-null FileSettings value
            if (_mockFileSettings.Object.Value == null)
            {
                var fileSettings = new FileSettings { IngredientFilePath = Path.Combine(Path.GetTempPath(), "default-ingredients.json") };
                _mockFileSettings.Setup(o => o.Value).Returns(fileSettings);
            }
            
            return new IngredientMappingLoader(_mockFileSettings.Object, _mockEnvironment.Object, NullLogger<IngredientMappingLoader>.Instance);
        }

        [Fact]
        public void LoadMappingsFileNotFoundThrowsFileNotFoundException()
        {
            var loader = CreateLoader();
            var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
            var fileSettings = new FileSettings { IngredientFilePath = nonExistentFile };
            _mockFileSettings.Setup(o => o.Value).Returns(fileSettings);
            Assert.Throws<FileNotFoundException>(() => loader.LoadMappings());
        }

        [Fact]
        public void LoadMappingsWithValidFileReturnsCorrectDictionary()
        {
            var mappings = new List<IngredientMapping>
            {
                new IngredientMapping { ProductId = "PRD-00001", Ingredients = new List<IngredientInfo> { new IngredientInfo { IngredientName = "Cheese", Amount = 1.5m } } },
                new IngredientMapping { ProductId = "PRD-00002", Ingredients = new List<IngredientInfo> { new IngredientInfo { IngredientName = "Tomato", Amount = 2.0m } } }
            };
            var json = JsonSerializer.Serialize(mappings);
            CreateTempIngredientFile(json);

            var loader = CreateLoader();
            var dict = loader.LoadMappings();

            Assert.Equal(2, dict.Count);
            Assert.True(dict.ContainsKey("PRD-00001"));
            var p1List = dict["PRD-00001"];
            Assert.Single(p1List);
            Assert.Equal("Cheese", p1List[0].IngredientName);
            Assert.Equal(1.5m, p1List[0].Amount);

            Assert.True(dict.ContainsKey("PRD-00002"));
            var p2List = dict["PRD-00002"];
            Assert.Single(p2List);
            Assert.Equal("Tomato", p2List[0].IngredientName);
            Assert.Equal(2.0m, p2List[0].Amount);
        }

        [Fact]
        public void LoadMappingsWithMalformedJsonThrowsInvalidDataException()
        {
            CreateTempIngredientFile("not valid json");
            var loader = CreateLoader();
            Assert.Throws<InvalidDataException>(() => loader.LoadMappings());
        }

        [Fact]
        public void LoadMappingsWithEmptyJsonArrayReturnsEmptyDictionary()
        {
            CreateTempIngredientFile("[]");
            var loader = CreateLoader();
            var dict = loader.LoadMappings();
            Assert.Empty(dict);
        }

        [Fact]
        public void LoadMappingsWithEmptyFileThrowsInvalidDataException()
        {
            CreateTempIngredientFile("");
            var loader = CreateLoader();
            Assert.Throws<InvalidDataException>(() => loader.LoadMappings());
        }

        [Fact]
        public void LoadMappingsWithEmptyJsonObjectThrowsInvalidDataException()
        {
            CreateTempIngredientFile("{}");
            var loader = CreateLoader();
            Assert.Throws<InvalidDataException>(() => loader.LoadMappings());
        }

        [Fact]
        public void LoadMappingsWithTypeMismatchThrowsInvalidDataException()
        {
            var json = "[{\"ProductId\": \"PRD-00001\", \"Ingredients\": [{\"IngredientName\": \"Dough\", \"Amount\": \"one\"}]}]";
            CreateTempIngredientFile(json);
            var loader = CreateLoader();
            Assert.Throws<InvalidDataException>(() => loader.LoadMappings());
        }

        [Fact]
        public void LoadMappingsWithDuplicateProductIdsThrowsInvalidDataException()
        {
            var mappings = new List<IngredientMapping>
            {
                new IngredientMapping { ProductId = "PRD-00001", Ingredients = new List<IngredientInfo>() },
                new IngredientMapping { ProductId = "PRD-00001", Ingredients = new List<IngredientInfo>() }
            };
            var json = JsonSerializer.Serialize(mappings);
            CreateTempIngredientFile(json);
            var loader = CreateLoader();
            Assert.Throws<InvalidDataException>(() => loader.LoadMappings());
        }

        [Fact]
        public void LoadMappingsWithNullIngredientsThrowsInvalidDataException()
        {
            // The IngredientMappingLoader was updated to initialize null Ingredients with an empty list
            // instead of throwing an exception, so we need to update this test
#pragma warning disable CS8625 // Intentional null for testing invalid data
            var mapping = new IngredientMapping { ProductId = "PRD-00001", Ingredients = null };
#pragma warning restore CS8625
            var json = JsonSerializer.Serialize(new List<IngredientMapping> { mapping });
            CreateTempIngredientFile(json);
            var loader = CreateLoader();
            
            // Instead of expecting an exception, verify it returns a dictionary with an empty list
            var result = loader.LoadMappings();
            Assert.Single(result);
            Assert.Empty(result["PRD-00001"]);
        }

        [Fact]
        public void LoadMappingsWithEmptyIngredientListReturnsEmptyList()
        {
            var mapping = new IngredientMapping { ProductId = "PRD-00001", Ingredients = new List<IngredientInfo>() };
            var json = JsonSerializer.Serialize(new List<IngredientMapping> { mapping });
            CreateTempIngredientFile(json);
            var loader = CreateLoader();
            var dict = loader.LoadMappings();
            Assert.Single(dict);
            Assert.True(dict.ContainsKey("PRD-00001"));
            Assert.Empty(dict["PRD-00001"]);
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
