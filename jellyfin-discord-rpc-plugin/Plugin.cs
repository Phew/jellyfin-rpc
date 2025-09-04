using System;
using System.IO;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.DiscordRpc;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string PluginName = "Discord RPC";
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => PluginName;

    public override Guid Id => new Guid("7f1e77a0-6e64-4b3c-9a78-2f6f3e23f2f6");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var htmlRes = GetType().Namespace + ".Configuration.config.html";
        var jsRes = GetType().Namespace + ".Configuration.config.js";
        return new[]
        {
            new PluginPageInfo { Name = "discordrpc", EmbeddedResourcePath = htmlRes },
            new PluginPageInfo { Name = "discordrpc.html", EmbeddedResourcePath = htmlRes },
            new PluginPageInfo { Name = "config.html", EmbeddedResourcePath = htmlRes },
            new PluginPageInfo { Name = "discordrpc.js", EmbeddedResourcePath = jsRes },
            new PluginPageInfo { Name = "config.js", EmbeddedResourcePath = jsRes }
        };
    }
}

