using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DiscordRpc;

public class PluginConfiguration : BasePluginConfiguration
{
    public string DetailsTemplate { get; set; } = "{title}";
    public string StateTemplate { get; set; } = "{season_episode} {progress_percent}%";
    public string LargeImageKey { get; set; } = "jellyfin";
    public string LargeImageTextTemplate { get; set; } = "Jellyfin";
    public string SmallImageKey { get; set; } = "play";
    public string SmallImageTextTemplate { get; set; } = "{play_state}";
    public bool IncludeTimestamps { get; set; } = true;
}

