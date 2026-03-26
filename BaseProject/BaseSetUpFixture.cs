using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace BaseProject
{
    [SetUpFixture]
    public abstract class BaseSetUpFixture
    {
        public static ILogger<BaseSetUpFixture> Logger { get; private set; }
        public static IHost Host { get; private set; }
        public static IServiceProvider ServiceProvider { get; private set; }
        public static IHostBuilder HostBuilder { get; private set; }
        public static string EnvironmentName { get; private set; }
        public static IConfigurationRoot Configuration { get; private set; }

        [OneTimeSetUp]
        public void BaseOneTimeSetUp()
        {
            // Create host builder with defaults.
            HostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder();

            // Resolve environment name from TestContext parameters or environment variables.
            EnvironmentName = ResolveEnvironment();

            // Use the environment name during host building.
            HostBuilder.UseEnvironment(EnvironmentName);

            // Configure services and logging.
            ConfigureHostBuilder(HostBuilder);

            // Build the host and retrieve the ServiceProvider.
            Host = HostBuilder.Build();
            ServiceProvider = Host.Services;

            // Create logger instance from the dependency injection container.
            Logger = ServiceProvider.GetRequiredService<ILogger<BaseSetUpFixture>>();
            Logger.LogInformation($"*** ENVIRONMENT: {EnvironmentName} ***");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Clean up the logger and dispose the host.
            Log.CloseAndFlush();
            Host?.Dispose();
        }

        /// <summary>
        /// Override to extend logging configuration.
        /// </summary>
        /// <param name="loggerConfiguration">Serilog LoggerConfiguration instance</param>
        /// <returns>Configured LoggerConfiguration</returns>
        protected virtual LoggerConfiguration ConfigureLogging(LoggerConfiguration loggerConfiguration)
        {
            // Default: no more customization; override in subclass if needed.
            return loggerConfiguration;
        }

        /// <summary>
        /// Override to register additional services for tests.
        /// </summary>
        /// <param name="serviceCollection">IServiceCollection instance to register dependencies</param>
        public virtual void RegisterTestFacilities(IServiceCollection serviceCollection)
        {
            // Default implementation left empty; override in derived classes if needed.
        }

        /// <summary>
        /// Sets up the host builder for services and logging.
        /// </summary>
        /// <param name="builder">The IHostBuilder instance.</param>
        private void ConfigureHostBuilder(IHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
                // Build configuration.
                Configuration = BuildConfiguration(EnvironmentName);

                // Register configuration options and test facilities.
                services.AddOptions();
                RegisterTestFacilities(services);

                // Register Serilog and logger dependencies.
                // Note: Using SerilogLoggerFactory to bridge Serilog to Microsoft.Extensions.Logging.
                Log.Logger = CreateSerilogLogger();
                services.AddSingleton<ILoggerFactory, SerilogLoggerFactory>();
                services.AddSingleton(Log.Logger);
                services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            });

            builder.ConfigureLogging((context, loggingBuilder) =>
            {
                // Clear default logging providers and add Serilog.
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(Log.Logger, dispose: true);
            });
        }

        /// <summary>
        /// Creates a Serilog logger based on the configuration.
        /// </summary>
        /// <returns>A configured ILogger instance.</returns>
        private Serilog.ILogger CreateSerilogLogger()
        {
            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Information();

            // Allow further configuration by derived classes.
            loggerConfig = ConfigureLogging(loggerConfig);

            return loggerConfig.CreateLogger();
        }

        /// <summary>
        /// Builds the application configuration.
        /// </summary>
        /// <param name="environment">The environment name used to load environment-specific settings.</param>
        /// <returns>An IConfigurationRoot instance.</returns>
        private IConfigurationRoot BuildConfiguration(string environment)
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .Build();
        }

        /// <summary>
        /// Resolves the environment from NUnit TestContext parameters or system environment variables.
        /// </summary>
        /// <example>dotnet test -- TestRunParameters.Parameter(name="\""environment"\"", value="\""DEV"\"")</example>
        /// <returns>The environment name string.</returns>
        private static string ResolveEnvironment()
        {
            // Try NUnit test parameters first.
            var env = TestContext.Parameters["environment"];
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            // Fallback to system environment variables.
            env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("environment");

            // If still not specified, optionally default to a preset value (for example, "Development").
            return string.IsNullOrWhiteSpace(env) ? "Development" : env;
        }
    }

}