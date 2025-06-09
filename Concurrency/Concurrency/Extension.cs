using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Concurrency.Core
{
    public static class Extension
    {
        /// <summary>
        /// Registers a singleton AsyncResourcePool<T> for the specified resource type.
        /// </summary>
        /// <typeparam name="T">The type of resource to be managed in the pool.</typeparam>
        /// <param name="services">The IServiceCollection to add the service to.</param>
        /// <param name="resourcesFactory">A factory function that provides the collection of resources to be managed.
        /// This function is resolved from the service provider, allowing access to configuration or other services.</param>
        /// <returns>The original IServiceCollection for chaining.</returns>
        public static IServiceCollection AddAsyncResourcePool<T>(
            this IServiceCollection services,
            Func<IServiceProvider, IEnumerable<T>> resourcesFactory) where T : notnull
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (resourcesFactory == null) throw new ArgumentNullException(nameof(resourcesFactory));

            // Register the AsyncResourcePool<T> as a singleton. It is thread-safe and holds state
            // (the pool of resources) for the lifetime of the application.
            services.TryAddSingleton<AsyncResourcePool<T>>(sp =>
            {
                var resources = resourcesFactory(sp);
                return new AsyncResourcePool<T>(resources);
            });

            return services;
        }
    }
}
