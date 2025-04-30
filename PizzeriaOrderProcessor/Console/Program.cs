using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;
using Serilog;
using Serilog.Events;
using PizzeriaOrderProcessor.Infrastructure.DataAccess;
using PizzeriaOrderProcessor.Infrastructure.Queue;
using PizzeriaOrderProcessor.Application.Services.Interfaces;
using PizzeriaOrderProcessor.Application.Services.Implementations;
using PizzeriaOrderProcessor.Validators;

namespace PizzeriaOrderProcessor.Console
{
    public class Program
    {
        // Protected constructor to prevent instantiation (satisfies SonarQube)
        protected Program() { }

        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Pizzeria Order Processor console application - Processes batches of pizza orders from JSON files.");
            
            // Add file paths argument
            var filePathsArgument = new Argument<string[]>(
                "files", 
                description: "Paths to order JSON files to process",
                getDefaultValue: () => Array.Empty<string>());
            rootCommand.AddArgument(filePathsArgument);

            rootCommand.SetHandler(async (context) =>
            {
                var cancellationToken = context.GetCancellationToken();
                var filePaths = context.ParseResult.GetValueForArgument(filePathsArgument);
                await RunApplicationLogic(filePaths, cancellationToken);
            });

            return await rootCommand.InvokeAsync(args);
        }

        // Main application logic using Generic Host
        private static async Task RunApplicationLogic(string[] filePaths, CancellationToken cancellationToken)
        {
            ConfigureBootstrapLogger();

            try
            {
                Log.Information("Configuring host...");

                // *** Determine Content Root Path ***
                var contentRootPath = AppContext.BaseDirectory;
                // *** End ***

                var host = BuildHost(contentRootPath);

                Log.Information("Host configured. Starting application run...");

                // Resolve the main orchestrator service
                // Using a scope to manage lifetime of scoped services if any
                using (var serviceScope = host.Services.CreateScope())
                {
                    await ProcessOrderFiles(serviceScope.ServiceProvider, filePaths, cancellationToken);
                }

                Log.Information("Application run completed.");
            }
            catch (Exception ex) // Catch errors during host configuration/build
            {
                 Log.Fatal(ex, "Host configuration or startup failed.");
            }
            finally
            {
                // Ensure logs are flushed before exit
                 await Log.CloseAndFlushAsync();
            }
        }

        private static void ConfigureBootstrapLogger()
        {
            Log.Logger = new LoggerConfiguration()
                 .MinimumLevel.Debug() 
                 .MinimumLevel.Override("Microsoft", LogEventLevel.Information) 
                 .Enrich.FromLogContext()
                 .WriteTo.Console() 
                 .CreateBootstrapLogger();
        }

        private static IHost BuildHost(string contentRootPath)
        {
            return Host.CreateDefaultBuilder() // Uses appsettings.json, env vars, etc.
                .UseContentRoot(contentRootPath) 
                .UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration) 
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    // Filter out validation details from console output
                    .WriteTo.Logger(lc => lc
                        .Filter.ByExcluding(e => e.Properties.ContainsKey("ValidationDetails"))
                        .Filter.ByExcluding(e => e.Level == LogEventLevel.Warning)
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}",
                            restrictedToMinimumLevel: LogEventLevel.Information))
                    // Write everything to file
                    .WriteTo.File(
                        Path.Combine("PizzeriaOrderProcessor", "logs", "pizzeria-.log"), 
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        restrictedToMinimumLevel: LogEventLevel.Debug) 
                 )
                .ConfigureServices(ConfigureServices)
                .Build();
        }

        private static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
        {
            // --- Configure Options ---
            services.Configure<FileSettings>(hostContext.Configuration.GetSection(FileSettings.SectionName));
            services.Configure<VatSettings>(hostContext.Configuration.GetSection(VatSettings.SectionName));
            services.Configure<ValidationSettings>(hostContext.Configuration.GetSection(ValidationSettings.SectionName));
            services.Configure<TimeSettings>(hostContext.Configuration.GetSection(TimeSettings.SectionName));
            services.Configure<OperatingHoursSettings>(hostContext.Configuration.GetSection(OperatingHoursSettings.SectionName));
            services.Configure<QueueSettings>(hostContext.Configuration.GetSection(QueueSettings.SectionName));

            services.PostConfigure<VatSettings>(settings => 
            {
                if (settings.DefaultFallbackValue != 0m && settings.DefaultFallbackValue != 0.05m)
                {
                    throw new InvalidOperationException("VAT rate must be either 0 or 0.05");
                }
            });

            // --- Register Core Services ---
            services.AddSingleton<IClock, SystemClock>();

            // --- Register Data Loaders (Singleton) ---
            services.AddSingleton<IProductCatalogLoader, ProductCatalogLoader>();
            services.AddSingleton<IIngredientMappingLoader, IngredientMappingLoader>();

            // --- Register Loaded Data as Singletons (using factory methods) ---
             services.AddSingleton(sp =>
             {
                 var loader = sp.GetRequiredService<IProductCatalogLoader>();
                 var logger = sp.GetRequiredService<ILogger<Program>>(); 
                 try
                 {
                     logger.LogInformation("Loading Product Catalog...");
                     var catalog = loader.LoadCatalog();
                     logger.LogInformation("Product Catalog loaded successfully.");
                     return catalog;
                 }
                 catch (Exception ex)
                 {
                     logger.LogCritical(ex, "Failed to load Product Catalog during startup. Application cannot proceed.");
                     throw new InvalidOperationException("Failed to load critical product catalog.", ex);
                 }
             });
             services.AddSingleton(sp =>
             {
                 var loader = sp.GetRequiredService<IIngredientMappingLoader>();
                  var logger = sp.GetRequiredService<ILogger<Program>>();
                 try
                 {
                     logger.LogInformation("Loading Ingredient Mappings...");
                     var mappings = loader.LoadMappings();
                     logger.LogInformation("Ingredient Mappings loaded successfully.");
                     return mappings;
                 }
                 catch (Exception ex)
                 {
                      logger.LogCritical(ex, "Failed to load Ingredient Mappings during startup. Application cannot proceed.");
                     throw new InvalidOperationException("Failed to load critical ingredient mappings.", ex);
                 }
             });


            // --- Register Processing Services ---
            services.AddTransient<IOrderBatchProcessor, OrderBatchProcessor>();
            services.AddTransient<IValidator<OrderItem>, OrderItemValidator>();
            services.AddTransient<IValidator<Order>, OrderValidator>();
            services.AddSingleton<IOrderCalculator, OrderCalculator>();
            services.AddSingleton<IIngredientAggregator, IngredientAggregator>();
            services.AddSingleton<IMockQueue>(sp =>
                new MockQueue(sp.GetRequiredService<ILogger<MockQueue>>()));
            services.AddTransient<IBatchQueuePusher, BatchQueuePusher>();
            services.AddSingleton<ISummaryReporter, TextSummaryReporter>();
            
            // Register the dependencies container
            services.AddScoped(sp => new OrderProcessingOrchestrator.OrderProcessingDependencies
            {
                ProductCatalog = sp.GetRequiredService<FrozenDictionary<string, Product>>(),
                IngredientMappings = sp.GetRequiredService<FrozenDictionary<string, List<IngredientInfo>>>(),
                OrderValidator = sp.GetRequiredService<IValidator<Order>>(),
                OrderCalculator = sp.GetRequiredService<IOrderCalculator>(),
                IngredientAggregator = sp.GetRequiredService<IIngredientAggregator>(),
                BatchQueuePusher = sp.GetRequiredService<IBatchQueuePusher>(),
                SummaryReporter = sp.GetRequiredService<ISummaryReporter>()
            });
            
            services.AddScoped<IOrderProcessingOrchestrator, OrderProcessingOrchestrator>(); 
        }

        private static async Task ProcessOrderFiles(IServiceProvider services, string[] filePaths, CancellationToken cancellationToken)
        {
            try
            {
                //load the data dictionaries here to catch loading errors before processing starts
                 _ = services.GetRequiredService<FrozenDictionary<string, Product>>();
                 _ = services.GetRequiredService<FrozenDictionary<string, List<IngredientInfo>>>();

                var logger = services.GetRequiredService<ILogger<Program>>();
                IEnumerable<string> targetFilePaths;

                if (filePaths.Length > 0)
                {
                    // Use the provided file paths
                    targetFilePaths = filePaths.Where(File.Exists).ToList();
                    
                    // Log warnings for any files that don't exist
                    foreach (var path in filePaths.Where(p => !File.Exists(p)))
                    {
                        logger.LogWarning("File not found: {FilePath}", path);
                    }
                    
                    logger.LogInformation("Processing {Count} specified order files", targetFilePaths.Count());
                }
                else
                {
                    // Fall back to scanning the directory for backward compatibility
                    var inputDirectoryPath = AppContext.BaseDirectory;
                    
                    if (!Directory.Exists(inputDirectoryPath))
                    {
                        logger.LogError("Input directory not found: {DirectoryPath}. Please create the directory.", inputDirectoryPath);
                        return;
                    }
                    
                    targetFilePaths = Directory.EnumerateFiles(inputDirectoryPath, "order*.json");
                    logger.LogInformation("No files specified. Found {Count} order JSON files in directory: {DirectoryPath}", 
                        targetFilePaths.Count(), inputDirectoryPath);
                }

                if (!targetFilePaths.Any())
                {
                    logger.LogWarning("No order JSON files found to process.");
                    return;
                }

                var orchestrator = services.GetRequiredService<IOrderProcessingOrchestrator>();
                await orchestrator.ProcessOrderBatchAsync(targetFilePaths, cancellationToken);
                await Task.Delay(5000);
            }
            catch (OperationCanceledException ex)
            {
                 Log.Warning(ex, "Processing was cancelled by user request.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("critical"))
            {
                 Log.Fatal(ex, "Application startup failed due to critical data loading error (caught after build).");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unexpected error occurred during order processing execution.");
            }
        }
    }
}
