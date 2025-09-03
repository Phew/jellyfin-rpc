# Jellyfin Discord RPC Plugin + CLI

This project provides:

- A Jellyfin plugin that exposes a secure endpoint returning a user-customizable Discord Rich Presence payload based on the current playback state.
- A simple Python CLI that polls the plugin endpoint every few seconds and updates your Discord Rich Presence.

## Plugin Features

- Per-server configuration for presence templates (details, state, images, tooltips, timestamps)
- Secure: Uses Jellyfin's built-in auth; endpoint only returns data for the calling user
- Endpoint: `GET /Plugins/DiscordRpc/Presence/Me`
 - Healthcheck: `GET /Plugins/DiscordRpc/Ping`

Response example:

```json
{
  "active": true,
  "details": "Spirited Away",
  "state": "S01E03 42%",
  "large_image": "jellyfin",
  "large_text": "Jellyfin",
  "small_image": "play",
  "small_text": "Playing",
  "start_timestamp": 1710000000,
  "is_paused": false,
  "user_id": "..."
}
```

If nothing is playing, the plugin returns:

```json
{ "active": false }
```

## Build and Install the Plugin

1. Prereqs: .NET 8 SDK
2. Build:

```bash
cd jellyfin-discord-rpc-plugin
dotnet publish -c Release -o ./bin/Release/net8.0/publish
```

3. Copy the generated DLLs from `./bin/Release/net8.0/publish` into your Jellyfin `plugins` directory under a folder named `DiscordRpc` (create if needed), then restart Jellyfin.

4. Configure in Jellyfin dashboard: Plugins → Discord RPC → set templates and save.

## CLI Usage (no parameters)

1. Prereqs: Python 3.10+
2. Install deps:

```bash
cd cli-app
pip install -r requirements.txt
```

3. Configure once (either):

- Create `cli-app/config.json` from `cli-app/config.json.example` and fill in values, or
- Place config at `%APPDATA%/JellyfinDiscordRPC/config.json` on Windows (or `~/.config/jellyfin-discord-rpc/config.json` on Linux/macOS).

4. Run:

```bash
python main.py
```

If no config is found, the app writes an example to the roaming config path and exits with instructions.

### Paused behavior

- When playing: CLI polls every few seconds with jitter to reduce load.
- When paused: CLI backs off to a random 30–45 seconds between polls.
- If paused for 3+ minutes: CLI clears Discord Rich Presence until playback resumes.

### Multi-user notes

- Each user runs the CLI locally with their own API key.
- The plugin selects the most relevant session per user (prefers active playing, then latest activity).

### Server-side installer

To build and install the plugin into the server's plugins directory and attempt to restart Jellyfin:

```bash
python server-tools/install_plugin.py
```

Override plugin directory via `JELLYFIN_PLUGIN_DIR` environment variable if auto-detection doesn't match your setup.

### Cover images via URLs (best-effort)

- The plugin returns `cover_image_path` for the item's primary image.
- The CLI builds a full URL and (optionally) appends your Jellyfin API key if `include_token_in_image_url` is true in `config.json`:

```json
{
  "include_token_in_image_url": true
}
```

Note: Official Discord clients may ignore URLs for Rich Presence images. If images don't appear, switch to pre-uploaded assets using the AssetKeyPrefix option in the plugin settings.

## Security Notes

- The plugin respects Jellyfin authentication and will only return presence for the authenticated user (identified by token).
- Use a user-scoped API key. Do not share your key.
- The CLI runs locally and only sends your token to your Jellyfin server over HTTPS if you provide an HTTPS URL.

## Customization Tokens

- `{title}`: Now playing item title
- `{season_episode}`: SxxEyy or Exx when available
- `{progress_percent}`: Integer percent progressed
- `{play_state}`: Playing or Paused

## Troubleshooting

- If the CLI shows Unauthorized, create a new Jellyfin API key for your user.
- Ensure the plugin is visible and enabled in Dashboard.
- Check Jellyfin server logs for `DiscordRpc` entries.


