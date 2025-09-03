using System;
using System.Linq;
using System.Security.Claims;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Jellyfin.Plugin.DiscordRpc.Controllers;

[ApiController]
[Authorize]
[Route("Plugins/DiscordRpc")] // GET /Plugins/DiscordRpc/Presence
public class PresenceController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly IUserManager _userManager;

    public PresenceController(ISessionManager sessionManager, IUserManager userManager)
    {
        _sessionManager = sessionManager;
        _userManager = userManager;
    }

    [HttpGet("Presence")] // Auth via Jellyfin token header
    public IActionResult GetPresence()
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { error = "Unauthorized" });
        }

        var sessions = _sessionManager.Sessions.ToList();
        var userSessions = sessions.Where(s => s.UserId == userId).ToList();
        var candidates = userSessions.Where(s => s.NowPlayingItem != null).ToList();
        if (candidates.Count == 0)
        {
            return Ok(new { active = false });
        }

        // Prefer actively playing over paused, then by last activity timestamp
        var session = candidates
            .OrderByDescending(s => (s.PlayState != null && s.PlayState.IsPaused == false) ? 1 : 0)
            .ThenByDescending(s => s.LastActivityDate ?? DateTime.MinValue)
            .First();

        var item = session.NowPlayingItem;
        var playState = session.PlayState;

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        var title = item.Name ?? "";
        var seasonEpisode = item.IndexNumber.HasValue
            ? (item.ParentIndexNumber.HasValue ? $"S{item.ParentIndexNumber:00}E{item.IndexNumber:00}" : $"E{item.IndexNumber:00}")
            : string.Empty;
        var progressPercent = playState?.PositionTicks.HasValue == true && item.RunTimeTicks.HasValue && item.RunTimeTicks.Value > 0
            ? (int)Math.Round(100.0 * playState.PositionTicks.Value / item.RunTimeTicks.Value)
            : 0;
        var isPaused = playState?.IsPaused == true;
        var playStateText = isPaused ? "Paused" : "Playing";

        string ReplaceTokens(string template)
        {
            return template
                .Replace("{title}", title)
                .Replace("{season_episode}", seasonEpisode)
                .Replace("{progress_percent}", progressPercent.ToString())
                .Replace("{play_state}", playStateText);
        }

        var details = ReplaceTokens(config.DetailsTemplate);
        var state = ReplaceTokens(config.StateTemplate);
        var largeText = ReplaceTokens(config.LargeImageTextTemplate);
        var smallText = ReplaceTokens(config.SmallImageTextTemplate);

        long? startTimestamp = null;
        if (config.IncludeTimestamps && playState?.PositionTicks.HasValue == true)
        {
            // Convert PositionTicks to a start time: now - position
            var position = TimeSpan.FromTicks(playState.PositionTicks.Value);
            var start = DateTimeOffset.UtcNow - position;
            startTimestamp = start.ToUnixTimeSeconds();
        }

        // Resolve cover image relative path for Primary if available
        string? coverPath = null;
        try
        {
            string? primaryTag = null;
            if (item.ImageTags != null && item.ImageTags.ContainsKey(ImageType.Primary))
            {
                primaryTag = item.ImageTags[ImageType.Primary];
            }
            else if (!string.IsNullOrEmpty(item.PrimaryImageTag))
            {
                primaryTag = item.PrimaryImageTag;
            }
            if (!string.IsNullOrEmpty(primaryTag) && item.Id != Guid.Empty)
            {
                coverPath = $"Items/{item.Id}/Images/Primary?tag={primaryTag}";
            }
        }
        catch { }

        // Optionally compute large image asset key based on item id
        var largeImageKey = config.LargeImageKey;
        if (config.UseItemCoverAsLargeImage && item.Id != Guid.Empty)
        {
            var normalizedId = item.Id.ToString("N"); // no dashes
            largeImageKey = config.AssetKeyPrefix + normalizedId;
        }

        return Ok(new
        {
            active = true,
            details,
            state,
            large_image = largeImageKey,
            large_text = largeText,
            small_image = config.SmallImageKey,
            small_text = smallText,
            start_timestamp = startTimestamp,
            is_paused = isPaused,
            user_id = userId,
            item_id = item.Id,
            item_type = item.Type?.ToString(),
            cover_image_path = coverPath
        });
    }

    [HttpGet("Presence/Me")] // Convenience alias
    public IActionResult GetPresenceMe()
    {
        return GetPresence();
    }

    [HttpGet("Ping")]
    [AllowAnonymous]
    public IActionResult Ping()
    {
        return Ok(new { ok = true, plugin = Plugin.PluginName });
    }

    private Guid GetCurrentUserId()
    {
        var idClaim = User?.Claims?.FirstOrDefault(c =>
            c.Type == ClaimTypes.NameIdentifier ||
            c.Type == "nameidentifier" ||
            c.Type == "sub" ||
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (Guid.TryParse(idClaim, out var id))
        {
            return id;
        }

        // Fallback: search an active session by header token
        var headerToken = Request.Headers["X-Emby-Token"].FirstOrDefault()
                           ?? Request.Headers["X-MediaBrowser-Token"].FirstOrDefault()
                           ?? ExtractBearerToken(Request.Headers["Authorization"].FirstOrDefault());
        if (!string.IsNullOrEmpty(headerToken))
        {
            var session = _sessionManager.Sessions.FirstOrDefault(s =>
                string.Equals((string?)s?.AccessToken, headerToken, StringComparison.Ordinal) ||
                string.Equals((string?)s?.Token, headerToken, StringComparison.Ordinal));
            if (session != null)
            {
                return session.UserId;
            }
        }

        return Guid.Empty;
    }

    private static string? ExtractBearerToken(string? authHeader)
    {
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            return null;
        }
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }
}

