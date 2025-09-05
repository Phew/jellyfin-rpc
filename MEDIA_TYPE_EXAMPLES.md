# Media Type Detection Examples

This document shows how the enhanced Jellyfin Discord RPC now properly differentiates between different media types and formats them accordingly.

## TV Shows / Episodes

### Before (All media showed the same format):
```
Details: "Breaking Bad"
State: "Crime, Drama, Thriller • 42:15 left"
```

### After (Episodes now show series + season/episode):
```
Details: "Breaking Bad S03E07"
State: "One Minute" • Crime, Drama, Thriller • 42:15 left
```

### Anime Examples:
```
Details: "Attack on Titan S04E16"
State: "Above and Below" • Animation, Action, Drama • 23:45 left

Details: "Demon Slayer S02E05"
State: "Move Forward!" • Animation, Action, Supernatural • 24:12 left
```

## Movies

### Format (unchanged, but now properly detected):
```
Details: "The Matrix"
State: "Action, Sci-Fi • 1:32:45 left"
```

### Another movie example:
```
Details: "Inception"
State: "Action, Drama, Sci-Fi • 2:08:22 left"
```

## Music

### Format:
```
Details: "Bohemian Rhapsody"
State: "Queen • Rock"
```

## Technical Implementation

### Plugin Configuration Templates

The plugin now uses different templates based on media type:

**Episodes:**
- Details: `{series_name} {season_episode}`
- State: `"{title}" • {genres} • {time_left}`

**Movies:**
- Details: `{title}`
- State: `{genres} • {time_left}`

**Music:**
- Details: `{title}`
- State: `{series_name} • {genres}`

### Media Type Detection

The system detects media types based on Jellyfin's item type:

- **Episode** → TV Show/Anime episode formatting
- **Movie** → Movie formatting  
- **Audio/MusicAlbum** → Music formatting
- **Other** → Default movie formatting

### Season/Episode Formatting

Episodes are consistently formatted as:
- `S01E05` (zero-padded)
- `S10E23` (handles multi-digit seasons/episodes)
- Falls back gracefully when season/episode data is incomplete

### Console Output

The CLI now shows the detected media type for debugging:

```
Jellyfin Discord RPC Selfbot (user: username)
Media Type: Episode
Breaking Bad S03E07
"One Minute" • Crime, Drama, Thriller • 42:15 left
```

```
Jellyfin Discord RPC Selfbot (user: username)  
Media Type: Movie
The Matrix
Action, Sci-Fi • 1:32:45 left
```

## Configuration

### Default Templates (Automatic)

The plugin comes with sensible defaults that differentiate between media types automatically. No configuration changes are needed for basic functionality.

### Custom Templates (Advanced)

You can customize templates in the Jellyfin plugin settings:

```json
{
  "Episodes": {
    "DetailsTemplate": "{series_name} {season_episode}",
    "StateTemplate": "\"{title}\" • {genres} • {time_left}"
  },
  "Movies": {
    "DetailsTemplate": "{title}",
    "StateTemplate": "{genres} • {time_left}"
  },
  "Music": {
    "DetailsTemplate": "{title}",
    "StateTemplate": "{series_name} • {genres}"
  }
}
```

### Available Template Variables

- `{title}` - Episode/Movie/Song title
- `{series_name}` - TV series name / Artist name
- `{season_episode}` - S01E05 format
- `{genres}` - Comma-separated genres
- `{time_left}` - Remaining time (MM:SS or HH:MM:SS)
- `{play_state}` - Playing/Paused
- `{progress_percent}` - Playback percentage

## Benefits

1. **Clear Differentiation**: TV shows and movies now display distinctly
2. **Better Information**: Episode titles and season/episode numbers are clearly shown
3. **Consistent Formatting**: All episodes use S01E05 format regardless of source
4. **Flexible Templates**: Easy to customize for different preferences
5. **Backward Compatibility**: Existing configurations continue to work

## Troubleshooting

### Episode Not Detected as TV Show

1. Check Jellyfin metadata - ensure the item type is "Episode"
2. Verify series name and season/episode numbers are populated
3. Check the CLI output for "Media Type: Episode"
4. Review Jellyfin logs for metadata issues

### Season/Episode Numbers Missing

1. Ensure your media files follow Jellyfin naming conventions
2. Check that metadata providers (TheTVDB, etc.) are enabled
3. Refresh metadata for the series in Jellyfin
4. Verify the plugin is receiving IndexNumber and ParentIndexNumber

### Custom Templates Not Working

1. Check plugin configuration in Jellyfin admin dashboard
2. Restart Jellyfin after configuration changes
3. Verify template syntax using available variables
4. Check Jellyfin plugin logs for errors
