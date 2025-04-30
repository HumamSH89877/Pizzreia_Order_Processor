using System.Collections.Frozen;
using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    public interface IIngredientMappingLoader
    {
        /// <summary>
        /// Loads ingredient mappings from the configured path.
        /// </summary>
        /// <returns>A FrozenDictionary mapping ProductId to a list of IngredientInfo.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the mapping file doesn't exist.</exception>
        /// <exception cref="InvalidDataException">Thrown if the mapping data is invalid (e.g., duplicates, bad JSON).</exception>
        /// <exception cref="IOException">Thrown on file read errors.</exception>
        FrozenDictionary<string, List<IngredientInfo>> LoadMappings();
    }
}
