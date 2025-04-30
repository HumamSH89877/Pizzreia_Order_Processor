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
    public class IngredientMappingLoader : IIngredientMappingLoader
    {
        private readonly ILogger<IngredientMappingLoader> _logger;
        private readonly string _mappingsFilePath; 
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public IngredientMappingLoader(
            IOptions<FileSettings> fileOptions,
            IHostEnvironment environment,
            ILogger<IngredientMappingLoader> logger)
        {
            _logger = logger;

            var relativePath = fileOptions.Value.IngredientFilePath;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                 _logger.LogCritical("IngredientFilePath is not configured in FileSettings.");
                 throw new InvalidOperationException("IngredientFilePath configuration is missing.");
            }
            _mappingsFilePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, relativePath));
            _logger.LogInformation("Resolved ingredient mappings path to: {FullPath}", _mappingsFilePath);

             _jsonSerializerOptions = new JsonSerializerOptions
             {
                 PropertyNameCaseInsensitive = true
             };
        }

        public FrozenDictionary<string, List<IngredientInfo>> LoadMappings()
        {
            ValidateFileExists();
            
            string json = ReadJsonFile();
            List<IngredientMapping> mappings = DeserializeJsonMappings(json);
            
            ValidateMappings(mappings);
            
            return CreateFrozenDictionary(mappings);
        }

        private void ValidateFileExists()
        {
            if (!File.Exists(_mappingsFilePath))
            {
                _logger.LogCritical("Ingredient mappings file not found: {Path}. Application might not function correctly without ingredient data.", _mappingsFilePath);
                throw new FileNotFoundException($"Ingredient mappings file not found: {_mappingsFilePath}", _mappingsFilePath);
            }
        }

        private string ReadJsonFile()
        {
            try
            {
                return File.ReadAllText(_mappingsFilePath);
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex, "Error reading ingredient mappings file: {Path}", _mappingsFilePath);
                throw new IOException($"Failed to read ingredient mappings file: {_mappingsFilePath}", ex);
            }
        }

        private List<IngredientMapping> DeserializeJsonMappings(string json)
        {
            try
            {
                var mappings = JsonSerializer.Deserialize<List<IngredientMapping>>(json, _jsonSerializerOptions) ?? new List<IngredientMapping>();
                _logger.LogInformation("Successfully deserialized {MappingCount} ingredient mappings from {Path}", mappings.Count, _mappingsFilePath);
                return mappings;
            }
            catch (JsonException ex)
            {
                _logger.LogCritical(ex, "Failed to parse ingredient mappings JSON file: {Path}. Check JSON structure and data types.", _mappingsFilePath);
                throw new InvalidDataException($"Failed to parse ingredient mappings JSON from {_mappingsFilePath}", ex);
            }
        }

        private void ValidateMappings(List<IngredientMapping> mappings)
        {
            ValidateNoDuplicateProductIds(mappings);
            
            foreach (var mapping in mappings)
            {
                ValidateProductIdFormat(mapping);
                ValidateIngredients(mapping);
            }
        }

        private void ValidateNoDuplicateProductIds(List<IngredientMapping> mappings)
        {
            var duplicateIds = mappings.GroupBy(m => m.ProductId)
                                      .Where(g => g.Count() > 1)
                                      .Select(g => g.Key)
                                      .ToList();
            if (duplicateIds.Any())
            {
                var duplicateIdList = string.Join(", ", duplicateIds);
                _logger.LogCritical("Duplicate ProductId(s) found in ingredient mappings {Path}: {DuplicateIds}", _mappingsFilePath, duplicateIdList);
                throw new InvalidDataException($"Duplicate ProductId(s) found in ingredient mappings {_mappingsFilePath}: {duplicateIdList}");
            }
        }

        private void ValidateProductIdFormat(IngredientMapping mapping)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(mapping.ProductId ?? "", @"^PRD-\d{5}$"))
            {
                _logger.LogWarning("Invalid ProductId format found: '{ProductId}' in {Path}. Expected 'PRD-NNNNN'.", mapping.ProductId, _mappingsFilePath);
                throw new InvalidDataException($"Invalid ProductId format '{mapping.ProductId}' found in {_mappingsFilePath}.");
            }
        }

        private void ValidateIngredients(IngredientMapping mapping)
        {
            if (mapping.Ingredients == null)
            {
                _logger.LogWarning("Ingredients list is null for ProductId {ProductId} in {Path}. This product will have no mapped ingredients.", mapping.ProductId, _mappingsFilePath);
                mapping.Ingredients = new List<IngredientInfo>(); // Ensure it's an empty list, not null
            }
            else if (mapping.Ingredients.Count > 5)
            {
                _logger.LogWarning("ProductId {ProductId} in {Path} has more than 5 ingredients ({Count}). Prompt specified max 5 for simplicity.", mapping.ProductId, _mappingsFilePath, mapping.Ingredients.Count);
            }

            if (mapping.Ingredients != null)
            {
                foreach (var ingredient in mapping.Ingredients)
                {
                    ValidateIngredient(ingredient, mapping.ProductId);
                }
            }
        }

        private void ValidateIngredient(IngredientInfo ingredient, string productId)
        {
            if (ingredient.Amount <= 0)
            {
                _logger.LogWarning("Invalid Amount ({Amount}) for Ingredient '{IngredientName}' for ProductId {ProductId} in {Path}. Amount should be positive.",
                    ingredient.Amount, ingredient.IngredientName, productId, _mappingsFilePath);
                
                throw new InvalidDataException($"Invalid Amount ({ingredient.Amount}) for Ingredient '{ingredient.IngredientName}' for ProductId {productId} in {_mappingsFilePath}.");
            }
            if (string.IsNullOrWhiteSpace(ingredient.IngredientName))
            {
                _logger.LogWarning("Missing IngredientName for ProductId {ProductId} in {Path}.", productId, _mappingsFilePath);
                
                throw new InvalidDataException($"Missing IngredientName for ProductId {productId} in {_mappingsFilePath}.");
            }
        }

        private FrozenDictionary<string, List<IngredientInfo>> CreateFrozenDictionary(List<IngredientMapping> mappings)
        {
            try
            {
                // Transform List<IngredientMapping> to the required FrozenDictionary format
                var frozenMappings = mappings.ToFrozenDictionary(
                    m => m.ProductId,
                    m => m.Ingredients ?? new List<IngredientInfo>(), 
                    StringComparer.Ordinal); 

                _logger.LogInformation("Ingredient mappings loaded and frozen successfully from {Path}. Contains mappings for {Count} unique products.", _mappingsFilePath, frozenMappings.Count);
                return frozenMappings;
            }
            catch (ArgumentException ex) 
            {
                _logger.LogCritical(ex, "Error creating frozen dictionary for ingredient mappings from {Path}.", _mappingsFilePath);
                throw new ArgumentException($"Failed to create frozen dictionary for ingredient mappings from {_mappingsFilePath}", ex);
            }
        }
    }
}
