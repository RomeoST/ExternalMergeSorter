using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Sorter.Abstractions;
using Sorter.Engines;
using Sorter.Engines.ExternalMerge;
using Sorter.Options;
using Sorter.Services;

namespace Sorter
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSorterOptions(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<SorterOptions>(config.GetSection("Sorter"));
            return services;
        }

        public static IServiceCollection AddSorterPolicies(this IServiceCollection services)
        {
            var registry = new PolicyRegistry
            {
                ["TransientRetry"] = Policy
                    .Handle<IOException>()
                    .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i))),

                ["MergeTimeout"] = Policy
                    .TimeoutAsync(TimeSpan.FromMinutes(5))
            };

            services.AddSingleton<IReadOnlyPolicyRegistry<string>>(registry)
                .AddSingleton<IPolicyRegistry<string>>(registry);

            return services;
        }

        public static IServiceCollection AddSorterServices(this IServiceCollection services)
        {
            services.AddSingleton<SortingCoordinator>()
                .AddSingleton<IChunkProducer, ChunkProducer>()
                .AddSingleton<IRunSorter, RunSorter>()
                .AddSingleton<IMerger, Merger>()
                .AddScoped<ISortingEngine, ExternalMergeSortingEngine>()
                .AddHostedService<ConfigPrinterService>()
                .AddHostedService<SorterBackgroundService>();

            return services;
        }
    }
}
