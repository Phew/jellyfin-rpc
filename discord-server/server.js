const { Client, RichPresence } = require('discord.js-selfbot-v13');
const express = require('express');
const axios = require('axios');
const cors = require('cors');
require('dotenv').config();

const app = express();
const port = process.env.PORT || 3001;

// Middleware
app.use(cors());
app.use(express.json());

// Discord client setup
const client = new Client();
let isDiscordReady = false;
let currentPresence = null;

// Discord token from environment variable
const DISCORD_TOKEN = process.env.DISCORD_TOKEN;

if (!DISCORD_TOKEN) {
    console.error('❌ DISCORD_TOKEN environment variable is required');
    process.exit(1);
}

// Discord client events
client.on('ready', async () => {
    console.log(`✅ Discord selfbot logged in as ${client.user.username}#${client.user.discriminator}`);
    isDiscordReady = true;
});

client.on('error', (error) => {
    console.error('❌ Discord client error:', error);
});

client.on('disconnect', () => {
    console.warn('⚠️ Discord client disconnected');
    isDiscordReady = false;
});

// Enhanced season/episode parsing for anime and TV shows
function parseSeasonEpisode(item) {
    const { IndexNumber, ParentIndexNumber, Name, SeriesName, Type } = item;
    
    let seasonNum = ParentIndexNumber;
    let episodeNum = IndexNumber;
    let displayName = Name || '';
    
    // For anime, try to extract season/episode from the title if not in metadata
    if (Type === 'Episode' && SeriesName) {
        // Common anime patterns: "S2E05", "Season 2 Episode 5", "2x05", etc.
        const patterns = [
            /S(\d+)E(\d+)/i,
            /Season\s*(\d+).*?Episode\s*(\d+)/i,
            /(\d+)x(\d+)/i,
            /\b(\d+)-(\d+)\b/,
        ];
        
        for (const pattern of patterns) {
            const match = displayName.match(pattern);
            if (match) {
                seasonNum = seasonNum || parseInt(match[1]);
                episodeNum = episodeNum || parseInt(match[2]);
                break;
            }
        }
    }
    
    // Format season/episode display
    if (seasonNum && episodeNum) {
        return `S${seasonNum.toString().padStart(2, '0')}E${episodeNum.toString().padStart(2, '0')}`;
    } else if (episodeNum) {
        return `E${episodeNum.toString().padStart(2, '0')}`;
    }
    
    return '';
}

// Enhanced presence formatting with media type detection
function formatPresenceData(data) {
    if (!data || !data.active) {
        return null;
    }
    
    const { details, state, item_type } = data;
    let formattedDetails = details;
    let formattedState = state;
    
    console.log(`📺 Processing media type: ${item_type}, Details: "${details}", State: "${state}"`);
    
    // The plugin now handles most of the formatting, but we can still enhance it here if needed
    // For TV shows and anime, the plugin should already be providing properly formatted data
    if (item_type === 'Episode') {
        console.log(`🎬 Detected TV Episode: ${formattedDetails}`);
        
        // Additional parsing for edge cases where plugin might miss season/episode info
        if (data.NowPlayingItem) {
            const seasonEpisode = parseSeasonEpisode(data.NowPlayingItem);
            const seriesName = data.NowPlayingItem.SeriesName || data.series_name;
            const episodeName = data.NowPlayingItem.Name || data.episode_title;
            
            // If the plugin didn't format it properly, do it here
            if (seasonEpisode && seriesName && !formattedDetails.includes(seasonEpisode)) {
                formattedDetails = `${seriesName} ${seasonEpisode}`;
                console.log(`🔧 Enhanced details: ${formattedDetails}`);
            }
            
            // Ensure episode title is in quotes in the state if not already formatted
            if (episodeName && episodeName !== seriesName && !formattedState.includes(`"${episodeName}"`)) {
                const stateParts = formattedState.split(' • ');
                const episodeQuoted = `"${episodeName}"`;
                if (!stateParts.includes(episodeQuoted)) {
                    stateParts.unshift(episodeQuoted);
                    formattedState = stateParts.join(' • ');
                    console.log(`🔧 Enhanced state: ${formattedState}`);
                }
            }
        }
    } else if (item_type === 'Movie') {
        console.log(`🎥 Detected Movie: ${formattedDetails}`);
    } else if (item_type === 'Audio' || item_type === 'MusicAlbum') {
        console.log(`🎵 Detected Music: ${formattedDetails}`);
    } else {
        console.log(`❓ Unknown media type: ${item_type}`);
    }
    
    return {
        details: formattedDetails,
        state: formattedState,
        largeImageKey: data.large_image || 'jellyfin',
        largeImageText: data.large_text || 'Jellyfin',
        smallImageKey: data.small_image || 'play',
        smallImageText: data.small_text || 'Playing',
        startTimestamp: data.start_timestamp ? new Date(data.start_timestamp * 1000) : null,
        endTimestamp: data.end_timestamp ? new Date(data.end_timestamp * 1000) : null,
        instance: false
    };
}

// API Routes
app.get('/health', (req, res) => {
    res.json({ 
        status: 'ok', 
        discord_ready: isDiscordReady,
        user: isDiscordReady ? `${client.user.username}#${client.user.discriminator}` : null
    });
});

app.post('/update-presence', async (req, res) => {
    try {
        if (!isDiscordReady) {
            return res.status(503).json({ error: 'Discord client not ready' });
        }

        const presenceData = formatPresenceData(req.body);
        
        if (!presenceData) {
            // Clear presence if no data
            if (currentPresence) {
                await client.user.setActivity();
                currentPresence = null;
                console.log('🧹 Cleared Discord presence');
            }
            return res.json({ success: true, action: 'cleared' });
        }

        // Create rich presence
        const rpc = new RichPresence()
            .setApplicationId(process.env.DISCORD_APPLICATION_ID || '1199810830972170261')
            .setType('WATCHING')
            .setDetails(presenceData.details)
            .setState(presenceData.state)
            .setAssetsLargeImage(presenceData.largeImageKey)
            .setAssetsLargeText(presenceData.largeImageText)
            .setAssetsSmallImage(presenceData.smallImageKey)
            .setAssetsSmallText(presenceData.smallImageText);

        if (presenceData.startTimestamp) {
            rpc.setStartTimestamp(presenceData.startTimestamp);
        }
        
        if (presenceData.endTimestamp) {
            rpc.setEndTimestamp(presenceData.endTimestamp);
        }

        await client.user.setActivity(rpc);
        currentPresence = presenceData;
        
        console.log(`🎵 Updated Discord presence: ${presenceData.details}`);
        res.json({ success: true, action: 'updated', presence: presenceData });
        
    } catch (error) {
        console.error('❌ Error updating presence:', error);
        res.status(500).json({ error: error.message });
    }
});

app.get('/current-presence', (req, res) => {
    res.json({ 
        presence: currentPresence,
        discord_ready: isDiscordReady 
    });
});

// Start server
app.listen(port, () => {
    console.log(`🚀 Discord selfbot server running on port ${port}`);
    console.log(`📊 Health check available at http://localhost:${port}/health`);
    
    // Login to Discord
    client.login(DISCORD_TOKEN).catch(error => {
        console.error('❌ Failed to login to Discord:', error);
        process.exit(1);
    });
});

// Graceful shutdown
process.on('SIGINT', () => {
    console.log('\n🛑 Shutting down Discord selfbot server...');
    if (client.user) {
        client.user.setActivity().catch(() => {});
    }
    client.destroy();
    process.exit(0);
});

process.on('SIGTERM', () => {
    console.log('\n🛑 Shutting down Discord selfbot server...');
    if (client.user) {
        client.user.setActivity().catch(() => {});
    }
    client.destroy();
    process.exit(0);
});
