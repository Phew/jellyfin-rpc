using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.DiscordRpc.Controllers;

[ApiController]
[AllowAnonymous]
[Route("Plugins/DiscordRpc/Cover")] // GET /Plugins/DiscordRpc/Cover/{itemId}?tag=...
public class CoverController : ControllerBase
{
    [HttpGet("{itemId}")]
    public async Task<IActionResult> GetCover([FromRoute] Guid itemId, [FromQuery] string? tag)
    {
        try
        {
            var imageManager = HttpContext.RequestServices.GetService(typeof(IImageManager)) as IImageManager;
            var libraryManager = HttpContext.RequestServices.GetService(typeof(ILibraryManager)) as ILibraryManager;
            if (imageManager == null || libraryManager == null)
            {
                return NotFound();
            }

            var item = libraryManager.GetItemById(itemId);
            if (item == null)
            {
                return NotFound();
            }

            // Fetch primary image stream
            var size = 512; // reasonable default size for Discord
            await using var stream = await imageManager.GetImageResponse(ImageType.Primary, item, size, size, tag, null, false, default);
            if (stream == null)
            {
                return NotFound();
            }

            // Let Jellyfin return proper content type; default to image/jpeg
            Response.ContentType = "image/jpeg";
            Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 minutes cache
            return File(stream, Response.ContentType);
        }
        catch
        {
            return NotFound();
        }
    }
}


