using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Jellyfin.Plugin.DiscordRpc.Controllers;

[ApiController]
[Authorize]
[Route("Plugins/DiscordRpc/Settings")] // Open: /Plugins/DiscordRpc/Settings?api_key=...
public class SettingsController : ControllerBase
{
    private const string PluginId = "7f1e77a0-6e64-4b3c-9a78-2f6f3e23f2f6";

    [HttpGet]
    public ContentResult Page()
    {
        var html = @"<!DOCTYPE html><html><head><meta charset='utf-8'><title>Discord RPC Settings</title>
<style>body{font-family:system-ui,Segoe UI,Roboto,Helvetica,Arial,sans-serif;margin:20px;max-width:800px}label{display:block;margin:10px 0 4px}input[type=text]{width:100%;padding:8px}code{background:#f2f2f2;padding:2px 4px;border-radius:4px}</style>
</head><body>
<h2>Discord RPC Settings (Direct)</h2>
<p>If the dashboard page isn\'t loading, you can edit settings here. Provide an API key in the URL query: <code>?api_key=YOUR_KEY</code>.</p>
<form id='f'>
  <label>Details Template</label>
  <input id='DetailsTemplate' type='text'/>
  <label>State Template</label>
  <input id='StateTemplate' type='text'/>
  <label>Large Image Key (fallback asset)</label>
  <input id='LargeImageKey' type='text'/>
  <label>Large Image Text Template</label>
  <input id='LargeImageTextTemplate' type='text'/>
  <label>Small Image Key</label>
  <input id='SmallImageKey' type='text'/>
  <label>Small Image Text Template</label>
  <input id='SmallImageTextTemplate' type='text'/>
  <label><input id='IncludeTimestamps' type='checkbox'/> Include timestamps</label>
  <label><input id='Images_ENABLE' type='checkbox'/> Enable Jellyfin image URLs</label>
  <label>Default Image Asset Key</label>
  <input id='DefaultImageAssetKey' type='text'/>
  <div style='margin-top:12px'>
    <button type='submit'>Save</button>
  </div>
</form>
<script>
const pluginId = '" + PluginId + @"';
const apiKey = new URLSearchParams(location.search).get('api_key');
const base = location.origin;
async function getCfg(){
  const r = await fetch(base+`/Plugins/Configuration/${pluginId}` + (apiKey?`?api_key=${apiKey}`:''));
  if(!r.ok){ alert('Failed to load config: '+r.status); return; }
  const c = await r.json();
  for(const k of ['DetailsTemplate','StateTemplate','LargeImageKey','LargeImageTextTemplate','SmallImageKey','SmallImageTextTemplate','DefaultImageAssetKey']){
    const el = document.getElementById(k); if(el && c[k]!=null) el.value = c[k];
  }
  document.getElementById('IncludeTimestamps').checked = !!c.IncludeTimestamps;
  if(c.Images){ document.getElementById('Images_ENABLE').checked = !!c.Images.ENABLE_IMAGES; }
}
document.getElementById('f').addEventListener('submit', async (e)=>{
  e.preventDefault();
  const c = {
    DetailsTemplate: document.getElementById('DetailsTemplate').value,
    StateTemplate: document.getElementById('StateTemplate').value,
    LargeImageKey: document.getElementById('LargeImageKey').value,
    LargeImageTextTemplate: document.getElementById('LargeImageTextTemplate').value,
    SmallImageKey: document.getElementById('SmallImageKey').value,
    SmallImageTextTemplate: document.getElementById('SmallImageTextTemplate').value,
    IncludeTimestamps: document.getElementById('IncludeTimestamps').checked,
    DefaultImageAssetKey: document.getElementById('DefaultImageAssetKey').value,
    Images: { ENABLE_IMAGES: document.getElementById('Images_ENABLE').checked }
  };
  const r = await fetch(base+`/Plugins/Configuration/${pluginId}` + (apiKey?`?api_key=${apiKey}`:''), {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(c)});
  if(r.ok){ alert('Saved'); } else { alert('Save failed: '+r.status); }
});
getCfg();
</script>
</body></html>";
        return Content(html, "text/html");
    }
}


