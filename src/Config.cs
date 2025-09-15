using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
using System.Text.Json.Serialization;

namespace GrenadeInfo
{
    public class InfoMessagesConfig
    {
        [JsonPropertyName("description")] public Dictionary<string, string> Description { get; set; } = [];
        [JsonPropertyName("sub_commands")] public Dictionary<string, InfoMessagesConfig> SubCommands { get; set; } = [];
    }

    public class PluginConfig : BasePluginConfig
    {
        // disabled
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        // debug prints
        [JsonPropertyName("debug")] public bool Debug { get; set; } = false;
        // info message limit
        [JsonPropertyName("info_message_limit")] public int InfoMessageLimit { get; set; } = 6;
        // show players personal stats after round
        [JsonPropertyName("show_personal_stats")] public bool ShowPersonalStats { get; set; } = true;
        // whether or not to show enemies flashed during round
        [JsonPropertyName("show_enemies_flashed")] public bool ShowEnemiesFlashed { get; set; } = true;
    }

    public partial class GrenadeInfo : BasePlugin, IPluginConfig<PluginConfig>
    {
        public required PluginConfig Config { get; set; }

        public void OnConfigParsed(PluginConfig config)
        {
            Config = config;
            // update config and write new values from plugin to config file if changed after update
            Config.Update();
            Console.WriteLine(Localizer["core.config"]);
        }
    }
}
