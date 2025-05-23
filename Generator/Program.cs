using Generator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;


var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddCommandLine(args)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30))
    .UseSerilog()
    .ConfigureServices((ctx, services) =>
    {
        services.AddGeneratorOptions(ctx.Configuration);
        services.AddGeneratorServices();
    })
    .Build();

try
{
    await host.RunAsync();
    return 0;
}
catch (OptionsValidationException ex)
{
    Log.Fatal( "Configuration error: {Errors}", string.Join("; ", ex.Failures));
    return 2;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}