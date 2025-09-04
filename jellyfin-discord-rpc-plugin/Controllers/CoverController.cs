using System;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.DiscordRpc.Controllers;

[ApiController]
[AllowAnonymous]
[Route("Plugins/DiscordRpc/Cover")] // GET /Plugins/DiscordRpc/Cover/{itemId}?tag=...
public class CoverController : ControllerBase
{
    [HttpGet("{itemId}")]
    public IActionResult GetCover([FromRoute] Guid itemId, [FromQuery] string? tag)
    {
        // Redirect to Jellyfin's built-in image endpoint; keep it anonymous
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = string.IsNullOrEmpty(tag)
            ? $"{baseUrl}/Items/{itemId}/Images/Primary"
            : $"{baseUrl}/Items/{itemId}/Images/Primary?tag={WebUtility.UrlEncode(tag)}";
        Response.Headers["Cache-Control"] = "public, max-age=300";
        return Redirect(url);
    }
}


