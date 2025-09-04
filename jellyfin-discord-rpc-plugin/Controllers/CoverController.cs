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
    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    [HttpGet("{itemId}")]
    public async Task<IActionResult> GetCover([FromRoute] Guid itemId, [FromQuery] string? tag)
    {
        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var url = string.IsNullOrEmpty(tag)
                ? $"{baseUrl}/Items/{itemId}/Images/Primary"
                : $"{baseUrl}/Items/{itemId}/Images/Primary?tag={WebUtility.UrlEncode(tag)}";

            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
            {
                return NotFound();
            }
            var contentType = resp.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
            Response.Headers["Cache-Control"] = "public, max-age=300";
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            return File(bytes, contentType);
        }
        catch
        {
            return NotFound();
        }
    }
}


