using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PizzeriaOrderProcessor.Configuration;

namespace PizzeriaOrderProcessor.Tests
{
    public class ConfigurationBindingTests
    {
        [Fact]
        public void FileSettingsBinding_ShouldBindValues()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["FileSettings:ProductFilePath"] = "prod.json",
                ["FileSettings:IngredientFilePath"] = "ing.json"
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var services = new ServiceCollection();
            services.Configure<FileSettings>(config.GetSection("FileSettings"));
            var serviceProvider = services.BuildServiceProvider();

            var settings = serviceProvider.GetService<IOptions<FileSettings>>()?.Value;

            Assert.NotNull(settings);
            Assert.Equal("prod.json", settings!.ProductFilePath);
            Assert.Equal("ing.json", settings.IngredientFilePath);
        }

        [Fact]
        public void OperatingHoursBinding_ShouldBindValues()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["OperatingHours:Open"] = "07:30:00",
                ["OperatingHours:Close"] = "21:45:00"
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var services = new ServiceCollection();
            services.Configure<OperatingHoursSettings>(config.GetSection("OperatingHours"));
            var serviceProvider = services.BuildServiceProvider();

            var hours = serviceProvider.GetService<IOptions<OperatingHoursSettings>>()?.Value;

            Assert.NotNull(hours);
            Assert.Equal("07:30:00", hours!.Open);
            Assert.Equal("21:45:00", hours.Close);
        }

        [Fact]
        public void ValidationSettingsBinding_ShouldBindValues()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["Validation:MinimumOrderAmount"] = "15.5",
                ["Validation:ItemQuantityUpperLimit"] = "200"
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var services = new ServiceCollection();
            services.Configure<ValidationSettings>(config.GetSection("Validation"));
            var serviceProvider = services.BuildServiceProvider();

            var vs = serviceProvider.GetService<IOptions<ValidationSettings>>()?.Value;

            Assert.NotNull(vs);
            Assert.Equal(15.5m, vs!.MinimumOrderAmount);
            Assert.Equal(200, vs.ItemQuantityUpperLimit);
        }

        [Fact]
        public void TimeSettingsBinding_ShouldBindValues()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["TimeSettings:PrepBufferBaseMinutes"] = "10",
                ["TimeSettings:PrepBufferPerItemMinutes"] = "3",
                ["TimeSettings:MaximumAdvanceOrderHours"] = "72"
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var services = new ServiceCollection();
            services.Configure<TimeSettings>(config.GetSection("TimeSettings"));
            var serviceProvider = services.BuildServiceProvider();

            var ts = serviceProvider.GetService<IOptions<TimeSettings>>()?.Value;

            Assert.NotNull(ts);
            Assert.Equal(10, ts!.PrepBufferBaseMinutes);
            Assert.Equal(3, ts.PrepBufferPerItemMinutes);
            Assert.Equal(72, ts.MaximumAdvanceOrderHours);
        }

        [Fact]
        public void VatSettingsBinding_ShouldBindValues()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["VatSettings:DefaultFallbackValue"] = "0.07"
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var services = new ServiceCollection();
            services.Configure<VatSettings>(config.GetSection("VatSettings"));
            var serviceProvider = services.BuildServiceProvider();

            var vs = serviceProvider.GetService<IOptions<VatSettings>>()?.Value;

            Assert.NotNull(vs);
            Assert.Equal(0.07m, vs!.DefaultFallbackValue);
        }
    }
}
