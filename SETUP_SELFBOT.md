# Jellyfin Discord RPC Selfbot Setup Guide

This guide shows how to set up the enhanced Jellyfin Discord RPC system that runs entirely on a Linux server using Discord selfbots, with improved season/episode display for TV shows and anime.

## ⚠️ Important Warning

**Using Discord selfbots violates Discord's Terms of Service and may result in account suspension or termination. Use this implementation at your own risk.**

## Overview

This enhanced system includes:
1. **Discord Selfbot Server** - Node.js server that manages Discord Rich Presence using `discord.js-selfbot-v13`
2. **Enhanced Jellyfin Plugin** - Provides detailed season/episode information
3. **Updated CLI Client** - Communicates with the selfbot server instead of direct Discord RPC

## Prerequisites

- Linux server with Node.js 20.18.0+
- Jellyfin 10.10+
- .NET 8 SDK (for plugin building)
- Python 3.10+ (for CLI client)
- Discord account and token

## Step 1: Set Up the Discord Selfbot Server

### 1.1 Install Node.js Dependencies
```bash
cd discord-server
npm install
```

### 1.2 Get Your Discord Token
1. Open Discord in your web browser
2. Press F12 to open Developer Tools
3. Go to Application tab → Local Storage → `https://discord.com/channels/@me`
4. Find the `token` key and copy its value (without quotes)

### 1.3 Configure Environment
```bash
cp env.example .env
nano .env
```

Add your Discord token:
```env
DISCORD_TOKEN=your_actual_discord_token_here
DISCORD_APPLICATION_ID=1199810830972170261
PORT=3001
```

### 1.4 Start the Server
```bash
npm start
```

You should see:
```
🚀 Discord selfbot server running on port 3001
📊 Health check available at http://localhost:3001/health
✅ Discord selfbot logged in as YourUsername#1234
```

## Step 2: Install the Enhanced Jellyfin Plugin

### 2.1 Build and Install Plugin
```bash
# Install the plugin (this builds and deploys automatically)
python3 server-tools/install_plugin.py
```

Or manually:
```bash
cd jellyfin-discord-rpc-plugin
dotnet publish -c Release -o ./bin/Release/net8.0/publish
# Copy to your Jellyfin plugins directory
cp -r ./bin/Release/net8.0/publish /path/to/jellyfin/plugins/DiscordRpc
```

### 2.2 Restart Jellyfin
```bash
sudo systemctl restart jellyfin
```

## Step 3: Configure the CLI Client

### 3.1 Install Python Dependencies
```bash
cd cli-app
pip3 install -r requirements.txt
```

### 3.2 Create Configuration
Create `cli-app/config.json`:
```json
{
  "jellyfin_url": "https://your.jellyfin.server",
  "api_key": "your_jellyfin_api_key",
  "username": "your_jellyfin_username",
  "discord_server_url": "http://localhost:3001",
  "interval": 5,
  "include_token_in_image_url": false,
  "Images": { "ENABLE_IMAGES": true }
}
```

### 3.3 Run the Enhanced CLI
```bash
python3 main_selfbot.py
```

## Step 4: Verify the Setup

### 4.1 Check Discord Server Status
```bash
curl http://localhost:3001/health
```

Should return:
```json
{
  "status": "ok",
  "discord_ready": true,
  "user": "YourUsername#1234"
}
```

### 4.2 Test Presence Update
Start playing something in Jellyfin and check your Discord profile. You should see enhanced information like:

**For TV Shows:**
- Details: "Breaking Bad S03E07"
- State: "One Minute" • Crime, Drama, Thriller • 42:15 left

**For Anime:**
- Details: "Attack on Titan S04E16"  
- State: "Above and Below" • Animation, Action, Drama • 23:45 left

## Enhanced Features

### Season/Episode Display Improvements
- **Consistent Formatting**: All episodes display as "S01E05" format
- **Series Recognition**: Episodes show series name + season/episode instead of just episode title
- **Episode Titles**: Episode names appear in the state line when available
- **Anime Support**: Enhanced parsing for various anime naming conventions
- **Fallback Handling**: Graceful degradation when metadata is incomplete

### Server Architecture Benefits
- **No Local Discord Required**: Runs entirely on your server
- **Better Reliability**: Dedicated server process handles Discord connection
- **Enhanced Logging**: Detailed logs for troubleshooting
- **API Interface**: RESTful API for presence management

## Running as a Service

### Discord Server Service
Create `/etc/systemd/system/jellyfin-discord-selfbot.service`:
```ini
[Unit]
Description=Jellyfin Discord Selfbot Server
After=network.target

[Service]
Type=simple
User=jellyfin
WorkingDirectory=/path/to/jellyfin-rpc-main/discord-server
ExecStart=/usr/bin/node server.js
Restart=always
RestartSec=10
Environment=NODE_ENV=production

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable jellyfin-discord-selfbot
sudo systemctl start jellyfin-discord-selfbot
```

### CLI Client Service
Create `/etc/systemd/system/jellyfin-rpc-client.service`:
```ini
[Unit]
Description=Jellyfin Discord RPC Client
After=network.target jellyfin-discord-selfbot.service
Requires=jellyfin-discord-selfbot.service

[Service]
Type=simple
User=jellyfin
WorkingDirectory=/path/to/jellyfin-rpc-main/cli-app
ExecStart=/usr/bin/python3 main_selfbot.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

## Troubleshooting

### Discord Connection Issues
```bash
# Check server logs
journalctl -u jellyfin-discord-selfbot -f

# Test Discord token manually
curl -H "Authorization: Bot YOUR_TOKEN" https://discord.com/api/v10/users/@me
```

### Plugin Issues
```bash
# Check Jellyfin logs
journalctl -u jellyfin -f

# Test plugin endpoint
curl "http://localhost:8096/Plugins/DiscordRpc/Ping"
```

### CLI Connection Issues
```bash
# Test Discord server connectivity
curl http://localhost:3001/health

# Check CLI logs
tail -f ~/.config/jellyfin-discord-rpc/rpc_selfbot.log
```

## Security Considerations

1. **Token Security**: Keep your Discord token secure and never commit to version control
2. **Network Security**: Consider firewall rules if exposing the Discord server
3. **Account Risk**: Monitor your Discord account for any unusual activity
4. **Regular Updates**: Keep dependencies updated for security patches

## Migration from Original Version

If migrating from the original direct Discord RPC version:

1. Stop the old CLI client
2. Set up the Discord selfbot server
3. Update your config to point to the selfbot server
4. Use `main_selfbot.py` instead of `main.py`
5. Install the enhanced plugin version

The enhanced version provides better season/episode display while maintaining compatibility with existing configurations.

## Support

For issues specific to:
- **Discord selfbot functionality**: Check `discord.js-selfbot-v13` documentation
- **Jellyfin integration**: Review Jellyfin plugin logs
- **Season/episode parsing**: Check the server logs for parsing details

Remember: Use of Discord selfbots is against Discord's Terms of Service. This implementation is provided for educational purposes and should be used at your own risk.
