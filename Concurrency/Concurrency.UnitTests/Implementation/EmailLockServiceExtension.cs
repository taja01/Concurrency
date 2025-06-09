using Concurrency.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Concurrency.UnitTests.Implementation
{
    public static class EmailLockServiceExtension
    {
        public static IServiceCollection AddEmailServicePool(
           this IServiceCollection services,
           IEnumerable<string> emailList)
        {
            if (emailList == null) throw new ArgumentNullException(nameof(emailList));
            var emails = emailList.ToList(); // Capture the list

            // Register the underlying AsyncResourcePool<string> using the generic helper.
            // We provide the list of emails directly.
            services.AddAsyncResourcePool(sp => emails);

            // Register the EmailService itself. It will get its required AsyncResourcePool<string>
            // dependency from the DI container. Scoped or Transient is usually appropriate for services.
            services.TryAddScoped<EmailLockService>();

            return services;
        }
    }
}
