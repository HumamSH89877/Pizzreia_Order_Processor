using System.Collections.Frozen;
using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Application.Services.Interfaces
{
    public interface IProductCatalogLoader
    {
        /// <summary>
        /// Loads the product catalog from the configured path.
        /// </summary>
        /// <returns>A FrozenDictionary mapping ProductId to Product.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the catalog file doesn't exist.</exception>
        /// <exception cref="InvalidDataException">Thrown if the catalog data is invalid (e.g., duplicates, bad JSON, negative prices).</exception>
        /// <exception cref="IOException">Thrown on file read errors.</exception>
        FrozenDictionary<string, Product> LoadCatalog();
    }
}
