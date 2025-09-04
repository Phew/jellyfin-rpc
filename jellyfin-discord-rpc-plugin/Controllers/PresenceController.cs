using System;
using System.Linq;
using System.Security.Claims;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;

namespace Jellyfin.Plugin.DiscordRpc.Controllers;

[ApiController]
[Authorize]
[Route("Plugins/DiscordRpc")] // GET /Plugins/DiscordRpc/Presence
public class PresenceController : ControllerBase
{

    [HttpGet("Presence")] // Auth via Jellyfin token header
    public async Task<IActionResult> GetPresence()
    {
        try
        {
            var sessionManager = HttpContext.RequestServices.GetService(typeof(ISessionManager)) as ISessionManager;
            if (sessionManager == null)
            {
                // Fallback: query server Sessions API via loopback
                return await GetPresenceViaHttpAsync();
            }
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            var sessions = sessionManager.Sessions.ToList();
            var userSessions = sessions.Where(s => s.UserId == userId).ToList();
            var candidates = userSessions.Where(s => s.NowPlayingItem != null).ToList();
            if (candidates.Count == 0)
            {
                return Ok(new { active = false });
            }

        // Prefer actively playing over paused, then by last activity timestamp
        var session = candidates
            .OrderByDescending(s => (s.PlayState != null && s.PlayState.IsPaused == false) ? 1 : 0)
            .ThenByDescending(s => s.LastActivityDate)
            .First();

        var item = session.NowPlayingItem;
        var playState = session.PlayState;

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        var title = item.Name ?? "";
        var seriesName = item.SeriesName ?? string.Empty;
        var itemType = item.Type.ToString();
        var genres = (item.Genres != null && item.Genres.Any()) ? string.Join(", ", item.Genres.Take(3)) : string.Empty;
        var seasonEpisode = item.IndexNumber.HasValue
            ? (item.ParentIndexNumber.HasValue ? $"S{item.ParentIndexNumber}E{item.IndexNumber}" : $"E{item.IndexNumber}")
            : string.Empty;
        var progressPercent = playState?.PositionTicks.HasValue == true && item.RunTimeTicks.HasValue && item.RunTimeTicks.Value > 0
            ? (int)Math.Round(100.0 * playState.PositionTicks.Value / item.RunTimeTicks.Value)
            : 0;
        var isPaused = playState?.IsPaused == true;
        var playStateText = isPaused ? "Paused" : "Playing";

        string ReplaceTokens(string template)
        {
            var mediaType = item.MediaType.ToString();
            var activity = string.Equals(mediaType, "Audio", StringComparison.OrdinalIgnoreCase) ? "Listening" : "Watching";
            var seriesOrTitle = string.IsNullOrEmpty(seriesName) ? title : seriesName;
            var seriesOrTitleWithCode = (!string.IsNullOrEmpty(seriesName) && !string.IsNullOrEmpty(seasonEpisode)) ? $"{seriesName} {seasonEpisode}" : seriesOrTitle;
            var episodeCodeTitle = title; // simple layout
            return template
                .Replace("{title}", title)
                .Replace("{season_episode}", seasonEpisode)
                .Replace("{progress_percent}", progressPercent.ToString())
                .Replace("{play_state}", playStateText)
                .Replace("{genres}", genres)
                .Replace("{series_name}", seriesName)
                .Replace("{time_left}", "")
                .Replace("{activity}", activity)
                .Replace("{series_or_title}", seriesOrTitleWithCode)
                .Replace("{episode_code_title}", episodeCodeTitle);
        }

        var details = ReplaceTokens(config.DetailsTemplate);
        var state = ReplaceTokens(config.StateTemplate);
        var largeText = ReplaceTokens(config.LargeImageTextTemplate);
        var smallText = ReplaceTokens(config.SmallImageTextTemplate);

        long? startTimestamp = null;
        long? endTimestamp = null;
        if (config.IncludeTimestamps && playState?.PositionTicks.HasValue == true)
        {
            // Convert PositionTicks to a start time: now - position
            var position = TimeSpan.FromTicks(playState.PositionTicks.Value);
            var start = DateTimeOffset.UtcNow - position;
            startTimestamp = start.ToUnixTimeSeconds();
            if (item.RunTimeTicks.HasValue && item.RunTimeTicks.Value > 0)
            {
                var remaining = TimeSpan.FromTicks(item.RunTimeTicks.Value) - position;
                var end = DateTimeOffset.UtcNow + remaining;
                endTimestamp = end.ToUnixTimeSeconds();
            }
        }

        // Compute time left string from endTimestamp if available and not paused
        string timeLeft = string.Empty;
        if (!isPaused && endTimestamp.HasValue)
        {
            var secondsLeft = (int)Math.Max(0, endTimestamp.Value - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var ts = TimeSpan.FromSeconds(secondsLeft);
            timeLeft = ts.Hours > 0 ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2} left" : $"{ts.Minutes:D2}:{ts.Seconds:D2} left";
        }

        // Re-apply time_left into state and details
        details = details.Replace("{time_left}", timeLeft);
        state = state.Replace("{time_left}", timeLeft);

        // Resolve cover image relative path for Primary if available
        string? coverPath = null;
        string? primaryTag = null;
        try
        {
            if (item.ImageTags != null && item.ImageTags.ContainsKey(ImageType.Primary))
            {
                primaryTag = item.ImageTags[ImageType.Primary];
            }
            // Prefer series poster for episodes
            Guid coverItemId = item.Id;
            if (itemType.Equals("Episode", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (!string.IsNullOrEmpty(item.SeriesId) && Guid.TryParse(item.SeriesId, out var sid))
                    {
                        coverItemId = sid;
                        // Try to use series primary tag when available
                        if (!string.IsNullOrEmpty(item.SeriesPrimaryImageTag))
                        {
                            primaryTag = item.SeriesPrimaryImageTag;
                        }
                    }
                }
                catch { }
            }
            if (coverItemId != Guid.Empty)
            {
                coverPath = string.IsNullOrEmpty(primaryTag)
                    ? $"Items/{coverItemId}/Images/Primary"
                    : $"Items/{coverItemId}/Images/Primary?tag={primaryTag}";
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

        // Build external info buttons (IMDb / TMDb) if available
        string? imdbUrl = null;
        string? tmdbUrl = null;
        try
        {
            if (item.ProviderIds != null)
            {
                if (item.ProviderIds.TryGetValue("Imdb", out var imdbRaw) && !string.IsNullOrWhiteSpace(imdbRaw))
                {
                    var imdbId = imdbRaw.StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? imdbRaw : ($"tt{imdbRaw}");
                    imdbUrl = $"https://www.imdb.com/title/{imdbId}/";
                }
                if (item.ProviderIds.TryGetValue("Tmdb", out var tmdbRaw) && !string.IsNullOrWhiteSpace(tmdbRaw))
                {
                    var isTv = itemType.Equals("Episode", StringComparison.OrdinalIgnoreCase) || itemType.Equals("Series", StringComparison.OrdinalIgnoreCase);
                    var tmdbType = isTv ? "tv" : "movie";
                    tmdbUrl = $"https://www.themoviedb.org/{tmdbType}/{tmdbRaw}";
                }
            }
        }
        catch { }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            string? publicCoverUrl = null;
            if (config.Images != null && config.Images.ENABLE_IMAGES && coverPath != null)
            {
                // Build direct Jellyfin URL with resize params
                var separator = coverPath.Contains("?") ? '&' : '?';
                publicCoverUrl = $"{baseUrl}/{coverPath}{separator}quality=90&fillHeight=512&fillWidth=512";
            }

            return Ok(new
            {
                active = true,
                details,
                state,
                large_image = publicCoverUrl ?? largeImageKey ?? config.DefaultImageAssetKey,
                large_text = largeText,
                small_image = config.SmallImageKey,
                small_text = smallText,
                start_timestamp = startTimestamp,
                end_timestamp = isPaused ? null : endTimestamp,
                is_paused = isPaused,
                user_id = userId,
                item_id = item.Id,
                item_type = itemType,
                cover_image_path = coverPath,
                public_cover_url = publicCoverUrl,
                links = new [] {
                    imdbUrl != null ? new { label = "IMDb", url = imdbUrl } : null,
                    tmdbUrl != null ? new { label = "TheMovieDb", url = tmdbUrl } : null
                }.Where(x => x != null)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("Presence/Me")] // Convenience alias
    public async Task<IActionResult> GetPresenceMe()
    {
        return await GetPresence();
    }

    [HttpGet("Ping")]
    [AllowAnonymous]
    public IActionResult Ping()
    {
        try
        {
            return Ok(new { ok = true, plugin = "Discord RPC" });
        }
        catch
        {
            // Always return a basic OK even if something unexpected happens
            return Ok(new { ok = true });
        }
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
        // No reliable token fallback in SessionInfo; require authenticated user context

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

    private async Task<IActionResult> GetPresenceViaHttpAsync()
    {
        try
        {
            var apiKey = Request.Headers["X-Emby-Token"].FirstOrDefault() ??
                         Request.Query["api_key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey))
            {
                return Unauthorized(new { error = "Missing token" });
            }

            // Build base URL from request
            var scheme = Request.Scheme;
            var host = Request.Host.Value;
            var baseUrl = $"{scheme}://{host}";

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            http.DefaultRequestHeaders.Add("X-Emby-Token", apiKey);

            // Get sessions and pick the first session for this token
            var url = baseUrl + "/Sessions" + $"?api_key={apiKey}";
            var resp = await http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                return Ok(new { active = false });
            }

            JsonElement? firstWithItem = null;
            foreach (var el in root.EnumerateArray())
            {
                if (el.TryGetProperty("NowPlayingItem", out var npi) && npi.ValueKind != JsonValueKind.Null)
                {
                    firstWithItem = el;
                    break;
                }
            }
            if (firstWithItem == null)
            {
                return Ok(new { active = false });
            }

            var sessionEl = firstWithItem.Value;
            var item = sessionEl.GetProperty("NowPlayingItem");
            var play = sessionEl.GetProperty("PlayState");

            string title = item.GetProperty("Name").GetString() ?? string.Empty;
            string seriesName = item.TryGetProperty("SeriesName", out var sn) ? (sn.GetString() ?? string.Empty) : string.Empty;
            int? index = item.TryGetProperty("IndexNumber", out var idx) && idx.TryGetInt32(out var iv) ? iv : (int?)null;
            int? pindex = item.TryGetProperty("ParentIndexNumber", out var pix) && pix.TryGetInt32(out var piv) ? piv : (int?)null;
            string seasonEpisode = index.HasValue ? (pindex.HasValue ? $"S{pindex:00}E{index:00}" : $"E{index:00}") : string.Empty;
            long? posTicks = play.TryGetProperty("PositionTicks", out var pt) && pt.TryGetInt64(out var pl) ? pl : (long?)null;
            long? runTicks = item.TryGetProperty("RunTimeTicks", out var rt) && rt.TryGetInt64(out var rl) ? rl : (long?)null;
            bool isPaused = play.TryGetProperty("IsPaused", out var ip) && ip.ValueKind == JsonValueKind.True;
            int progress = (posTicks.HasValue && runTicks.HasValue && runTicks.Value > 0) ? (int)Math.Round(100.0 * posTicks.Value / runTicks.Value) : 0;

            // Genres (top 3)
            string genres = string.Empty;
            if (item.TryGetProperty("Genres", out var gEl) && gEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                int count = 0;
                foreach (var ge in gEl.EnumerateArray())
                {
                    var gs = ge.GetString();
                    if (!string.IsNullOrEmpty(gs))
                    {
                        list.Add(gs);
                        count++;
                        if (count == 3) break;
                    }
                }
                genres = string.Join(", ", list);
            }

            long? startTs = null;
            long? endTs = null;
            if (posTicks.HasValue)
            {
                var position = TimeSpan.FromTicks(posTicks.Value);
                startTs = (DateTimeOffset.UtcNow - position).ToUnixTimeSeconds();
                if (runTicks.HasValue && runTicks.Value > 0)
                {
                    var remaining = TimeSpan.FromTicks(runTicks.Value) - position;
                    endTs = (DateTimeOffset.UtcNow + remaining).ToUnixTimeSeconds();
                }
            }

            // Simple layout: title/series on top, then genres, then time left
            string seriesOrTitle = string.IsNullOrEmpty(seriesName) ? title : seriesName;
            string details = seriesOrTitle;
            string timeLeft = string.Empty;
            if (!isPaused && endTs.HasValue)
            {
                var secondsLeft = Math.Max(0, endTs.Value - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                var ts = TimeSpan.FromSeconds(secondsLeft);
                timeLeft = ts.Hours > 0 ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2} left" : $"{ts.Minutes:D2}:{ts.Seconds:D2} left";
            }
            string state = string.IsNullOrEmpty(genres) ? timeLeft : ($"{genres}\n{timeLeft}").Trim();

            return Ok(new
            {
                active = true,
                details,
                state,
                start_timestamp = startTs,
                end_timestamp = isPaused ? null : endTs,
                is_paused = isPaused
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

