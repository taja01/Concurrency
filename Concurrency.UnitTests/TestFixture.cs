using BaseProject;
using Concurrency.UnitTests.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Concurrency.UnitTests
{
    [SetUpFixture]
    internal class TestFixture : BaseSetUpFixture
    {
        public override void RegisterTestFacilities(IServiceCollection serviceCollection)
        {
            base.RegisterTestFacilities(serviceCollection);


            var emails = new[] { "user@example.com", "concurrent@example.com" };
            serviceCollection.AddEmailServicePool(emails);
        }
    }
}
