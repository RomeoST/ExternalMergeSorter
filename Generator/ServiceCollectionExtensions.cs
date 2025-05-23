using Generator.Abstractions;
using Generator.Core;
using Generator.Infrastructure.IO;
using Generator.Infrastructure.WordProviders;
using Generator.Options;
using Generator.Pipelines;
using Generator.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Generator
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGeneratorOptions(this IServiceCollection services, IConfiguration config)
        {
            services.AddOptions<StorageOptions>()
                .Bind(config.GetSection("Storage"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<PipelineOptions>()
                .Bind(config.GetSection("Pipeline"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }

        public static IServiceCollection AddGeneratorServices(this IServiceCollection services)
        {
            // Core generation pipeline
            services.AddSingleton<IWordProvider, JsonWordProvider>();
            services.AddSingleton<PreEncodedWordPool>();
            services.AddSingleton<IProducer, LineProducer>();

            // IO components
            services.AddSingleton<IFileMerger, FileMerger>();
            services.AddSingleton<IConsumerManager, ConsumerManager>();
            services.AddSingleton<IProducerManager, ProducerManager>();

            // Hosted services
            services.AddHostedService<StartupValidator>();
            services.AddHostedService<FileGeneratorHost>();

            return services;
        }
    }
}
