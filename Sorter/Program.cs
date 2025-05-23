using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Registry; // Add this using directive for AddPolicyRegistry
using Serilog;
using Sorter.Abstractions;
using Sorter.Engines;
using Sorter.Engines.ExternalMerge;
using Sorter.Options;
using Sorter.Services;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddCommandLine(args)
    .Build();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30))
        .UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            }
            )
        .ConfigureServices((ctx, services) =>
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

            services.Configure<SorterOptions>(ctx.Configuration.GetSection("Sorter"))
                    .AddSingleton<SortingCoordinator>()
                    .AddSingleton<IChunkProducer, ChunkProducer>()
                    .AddSingleton<IRunSorter, RunSorter>()
                    .AddSingleton<IMerger, Merger>()
                    .AddScoped<ISortingEngine, ExternalMergeSortingEngine>()
                    .AddHostedService<ConfigPrinterService>()
                    .AddHostedService<SorterBackgroundService>();
        })
        .Build();

    Log.Information("Starting host...");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}