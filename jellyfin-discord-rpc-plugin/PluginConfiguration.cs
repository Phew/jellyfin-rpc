using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DiscordRpc;

public class PluginConfiguration : BasePluginConfiguration
{
    public string DetailsTemplate { get; set; } = "{series_or_title}";
    public string StateTemplate { get; set; } = "{genres} • {time_left}";
    public string LargeImageKey { get; set; } = "jellyfin";
    public string LargeImageTextTemplate { get; set; } = "Jellyfin";
    public string SmallImageKey { get; set; } = "play";
    public string SmallImageTextTemplate { get; set; } = "{play_state}";
    public bool IncludeTimestamps { get; set; } = true;

    // When enabled, large_image will be computed from the item's Id using AssetKeyPrefix + item.Id
    public bool UseItemCoverAsLargeImage { get; set; } = false;
    public string AssetKeyPrefix { get; set; } = "cover_";

    // Image settings group
    public ImagesConfig Images { get; set; } = new ImagesConfig();
    public string DefaultImageAssetKey { get; set; } = "jellyfin";
}

public class ImagesConfig
{
    public bool ENABLE_IMAGES { get; set; } = true;
}

