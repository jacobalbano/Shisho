using Microsoft.Extensions.Logging;
using Shisho.Utility;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shisho;

public class ConfigFile
{
    public string Token { get; init; } = "__CUSTOMIZE__";

    public int TickMilliseconds { get; init; } = 1000;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Information;
    
    public static ConfigFile Prepare()
    {
        if (File.Exists(ConfigFileName))
        {
            var contents = File.ReadAllText(ConfigFileName);
            var config = JsonSerializer.Deserialize<ConfigFile>(contents);
            if (config != null)
                return config;
        }

        try { File.WriteAllText(ConfigFileName, JsonSerializer.Serialize(new ConfigFile())); }
        finally
        {
            throw new Exception($"Unable to load a config file from '{ConfigFileName}'. A template file has been created.");
        }
    }

    private const string ConfigFileName = "config.json";
}
