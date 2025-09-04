# Jellyfin Discord RPC (Plugin + CLI)

Display what you're watching on Jellyfin as Discord Rich Presence. Direct Jellyfin artwork (no Imgur), simple installer, and username scoping so multi‑user servers don’t clash.

## Requirements
- Jellyfin 10.10+
- .NET 8 SDK (server build only)
- Python 3.10+ (for the CLI)
- Discord Application (copy its Client ID; the app name is the bold title in Discord)

## Quick Start (one‑command install)

1) Create the CLI config first (the installer reads this):

```json
{
  "jellyfin_url": "https://your.jellyfin",
  "api_key": "<YOUR_JELLYFIN_API_KEY>",
  "username": "<YOUR_JELLYFIN_USERNAME>",
  "discord_client_id": "<YOUR_DISCORD_APP_CLIENT_ID>",
  "interval": 5,
  "include_token_in_image_url": false,
  "Images": { "ENABLE_IMAGES": true }
}
```

2) Install/update the plugin on the server:

```bash
# Windows (PowerShell)
python server-tools/install_plugin.py

# Linux/macOS
python3 server-tools/install_plugin.py
```

What the installer does:
- Builds and deploys the plugin to Jellyfin’s `plugins/DiscordRpc`
- Restarts Jellyfin and waits for readiness
- Pushes sane defaults using `jellyfin_url` and `api_key` from `cli-app/config.json`
  - Alternatively set env vars `JELLYFIN_URL` and `JELLYFIN_API_KEY`

3) Run the CLI on your PC:

```bash
cd cli-app
pip install -r requirements.txt
python main.py
```

You’ll see: “Jellyfin Discord RPC (user: yourname)”. The RPC updates only when that user is playing.

## How It Works

- Presence:
  - `GET /Plugins/DiscordRpc/Presence/Me?api_key=...&username=yourname`
  - With `username`, the server filters to that user’s session (works even with admin API keys)
  - Without `username`, it uses the caller’s auth context if available
- Healthcheck:
  - `GET /Plugins/DiscordRpc/Ping`
- Artwork (Jellyfin only, no Imgur):
  - Direct URLs with resize params; episodes prefer the series poster
  - Example: `https://your.jellyfin/Items/{Id}/Images/Primary?quality=90&fillHeight=512&fillWidth=512[&tag=...]`

## Example Presence

```json
{
  "active": true,
  "details": "Doctor Who S1E1",
  "state": "Action, Drama, Mystery • 00:46:18 left",
  "start_timestamp": 1710000000,
  "end_timestamp": 1710000900,
  "is_paused": false,
  "public_cover_url": "https://your.server/Items/<Id>/Images/Primary?quality=90&fillHeight=512&fillWidth=512&tag=..."
}
```

If idle:
```json
{ "active": false }
```
![Example presence](https://files.catbox.moe/zyw0tf.png)

## CLI Behavior

- Details = title (or “Series SxEx”); State = “Genres • mm:ss left”
- Progress bar while playing
- Paused:
  - Poll backs off 30–45s
  - After 3 minutes paused, RPC clears; resumes when playing
- Updates terminal window title to the current item and shows scoped username

## Logging (enabled by default)

- File only (no console spam)
  - Windows: `%APPDATA%\JellyfinDiscordRPC\rpc.log`
  - macOS/Linux: `~/.config/jellyfin-discord-rpc/rpc.log`
- Logs include: username scope, exact image URL chosen, update attempts, and errors (with stack traces)

## Images: What to Expect

- The plugin/CLI always use Jellyfin artwork URLs
- Must be publicly reachable via HTTPS from the Discord client
  - Test in a private/incognito window; expect HTTP 200 and inline poster
- Some Discord clients ignore URL images
  - If placeholders appear, upload a 512×512 Discord app asset (e.g., `jellyfin`) and set that as the Large Image Key for a guaranteed fallback

## Manual Install (alternative)

1) Build:
```bash
cd jellyfin-discord-rpc-plugin
dotnet publish -c Release -o ./bin/Release/net8.0/publish
```

2) Copy publish output to the server’s `plugins/DiscordRpc` folder

3) Restart Jellyfin and hard‑refresh the dashboard (Ctrl+F5)

## Direct Settings Page (fallback)

If the dashboard page doesn’t load, use: http(s)://YOUR_SERVER/Plugins/DiscordRpc/Settings?api_key=YOUR_KEY

Edit and save settings there.

## Troubleshooting

- 401 Unauthorized: wrong/missing API key. Use user API key, or admin key plus `username`
- 500 on presence: check Jellyfin logs; try `/Plugins/DiscordRpc/Ping` first
- Images don’t show: open `public_cover_url` in private browser; must be HTTPS and 200 OK without auth/redirects
- Buttons not visible: Discord hides buttons on your own profile; ask a friend or check CLI logs
- Username mismatch: ensure `username` in `cli-app/config.json` matches your Jellyfin `UserName` (case‑insensitive)

## Security

- With `username`, the plugin never returns other users’ sessions
- The CLI only connects to your Jellyfin; prefer HTTPS
