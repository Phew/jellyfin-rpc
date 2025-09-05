# Jellyfin Discord Selfbot Server

This Node.js server enables Discord Rich Presence for Jellyfin using a Discord selfbot implementation with `discord.js-selfbot-v13`. This allows the system to run entirely on a Linux server without requiring Discord to be running locally.

## ⚠️ Important Disclaimer

**Using Discord selfbots is against Discord's Terms of Service and may result in account suspension or termination. Use this at your own risk.**

## Requirements

- Node.js 20.18.0 or newer
- A Discord account and token
- Discord Application ID (optional, defaults provided)

## Installation

1. **Install dependencies:**
```bash
cd discord-server
npm install
```

2. **Get your Discord token:**
   - Open Discord in your web browser
   - Press F12 to open Developer Tools
   - Go to Application tab → Local Storage → `https://discord.com/channels/@me`
   - Find the `token` key and copy its value (without quotes)

3. **Create environment file:**
```bash
cp env.example .env
```

4. **Edit the `.env` file:**
```env
DISCORD_TOKEN=your_discord_token_here
DISCORD_APPLICATION_ID=1199810830972170261  # Optional, defaults provided
PORT=3001  # Optional, defaults to 3001
```

## Usage

### Start the server:
```bash
npm start
```

### For development (with auto-restart):
```bash
npm run dev
```

The server will:
- Connect to Discord using your selfbot token
- Start an HTTP server on port 3001 (or your configured port)
- Accept presence updates from the Jellyfin CLI client
- Display enhanced season/episode information for TV shows and anime

## API Endpoints

- `GET /health` - Check server and Discord connection status
- `POST /update-presence` - Update Discord Rich Presence
- `GET /current-presence` - Get current presence data

## Enhanced Features

### Season/Episode Display
The server automatically enhances season and episode information:
- Formats episodes as "Series Name S01E05" 
- Adds episode titles to the state line
- Parses season/episode from various formats in anime titles
- Falls back gracefully when metadata is incomplete

### Example Output
Instead of just "Doctor Who":
```
Details: "Doctor Who S01E05"
State: "The Unquiet Dead" • Sci-Fi, Drama • 32:15 left
```

## Troubleshooting

### Discord Connection Issues
- Verify your Discord token is correct and not expired
- Check that your Discord account is not banned or restricted
- Ensure Node.js version is 20.18.0 or newer

### Server Not Starting
- Check if port 3001 is available or change the PORT in `.env`
- Verify all dependencies are installed with `npm install`
- Check the console output for specific error messages

### CLI Connection Issues
- Ensure the Discord server is running before starting the CLI
- Verify the `discord_server_url` in your CLI config points to the correct server
- Check firewall settings if running on different machines

## Security Considerations

- Keep your Discord token secure and never commit it to version control
- Consider running the server behind a reverse proxy if exposing to the internet
- Monitor Discord's Terms of Service for any changes regarding selfbots
- Use at your own risk - Discord may suspend accounts using selfbots

## Logs

Server logs include:
- Discord connection status
- Presence update attempts
- API request handling
- Error messages with stack traces

The server uses console output for real-time monitoring.
