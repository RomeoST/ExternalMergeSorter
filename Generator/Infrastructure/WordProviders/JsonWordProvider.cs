using Generator.Abstractions;
using Generator.Options;
using Microsoft.Extensions.Logging;
using System.Text;
using Microsoft.Extensions.Options;

namespace Generator.Infrastructure.WordProviders
{
    public sealed class JsonWordProvider : IWordProvider
    {
        private readonly StorageOptions _storageOptions;
        private readonly ILogger<JsonWordProvider> _log;

        public JsonWordProvider(ILogger<JsonWordProvider> log, IOptions<StorageOptions> storageOptions)
        {
            _log = log;
            _storageOptions = storageOptions.Value;
        }

        public string[] LoadWords()
        {
            var path = _storageOptions.WordListPath;
            if (!File.Exists(path))
            {
                _log.LogWarning("{Path} not found; fallback to built-in list", path);
                return new[] { "Apple", "Banana", "Cherry" };
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
                if (arr.Length == 0)
                    _log.LogWarning("File {Path} empty", path);

                return arr;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error reading {Path}; fallback", path);
                return new[] { "Apple", "Banana", "Cherry" };
            }
        }
    }
}
