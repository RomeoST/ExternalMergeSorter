using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Sorter;

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
            services.AddSorterOptions(ctx.Configuration)
                .AddSorterPolicies()
                .AddSorterServices();
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