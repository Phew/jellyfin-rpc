using System;
using System.IO;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DiscordRpc;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string PluginName = "Discord RPC";
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths) : base(applicationPaths)
    {
        Instance = this;
    }

    public override string Name => PluginName;

    public override Guid Id => new Guid("7f1e77a0-6e64-4b3c-9a78-2f6f3e23f2f6");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "discordrpc",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html"
            },
            new PluginPageInfo
            {
                Name = "discordrpcjs",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.js"
            }
        };
    }
}

