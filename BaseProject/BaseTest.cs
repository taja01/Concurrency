using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace BaseProject
{
    /// <summary> Base class for all tests. Retrieves common infrastructure such as the Host, ServiceProvider, 
    /// and configuration settings initialized in the BaseSetUpFixture. Also provides a logger that is 
    /// constructed for the derived test type. 
    /// </summary> 
    public abstract class BaseTest
    {
        /// <summary> 
        /// Gets the IHost instance from the BaseSetUpFixture. 
        /// </summary> 
        public IHost Host => BaseSetUpFixture.Host;

        /// <summary>
        /// Gets the IServiceProvider instance from the BaseSetUpFixture.
        /// </summary>
        public IServiceProvider ServiceProvider => BaseSetUpFixture.ServiceProvider;

        /// <summary>
        /// Gets the IHostBuilder instance from the BaseSetUpFixture.
        /// </summary>
        public IHostBuilder HostBuilder => BaseSetUpFixture.HostBuilder;

        /// <summary>
        /// Gets the current environment name.
        /// </summary>
        public string EnvironmentName => BaseSetUpFixture.EnvironmentName;

        /// <summary>
        /// Gets the IConfigurationRoot instance from the BaseSetUpFixture.
        /// </summary>
        public IConfigurationRoot Configuration => BaseSetUpFixture.Configuration;

        /// <summary>
        /// Gets the logger instance for the specific test type.
        /// </summary>
        public ILogger Logger { get; private set; }

        /// <summary>
        /// Sets up the test by retrieving a logger instance from the DI container.
        /// This method is executed before each test method in derived classes.
        /// </summary>
        [SetUp]
        public virtual void SetUpBase()
        {
            // Create a logger instance using the type of the derived test class.
            var loggerType = typeof(ILogger<>).MakeGenericType(GetType());
            Logger = ServiceProvider.GetRequiredService(loggerType) as ILogger
                ?? throw new InvalidOperationException($"Logger not registered for type {GetType().FullName}");
        }
    }
}