# 🎧 Pillar 1: Trải nghiệm Nghe nhạc Đắm chìm (Immersive UI)

VibeMusic delivers a premium "Glassmorphism" aesthetic combined with a high-performance streaming engine.

---

## ✨ Features

### 1. YouTube Audio Streaming
- **Experience**: Access to the global YouTube music library with near-instant buffering.
- **Technical**: Uses `YoutubeStreamResolver.cs` to extract direct audio-only streams, reducing bandwidth and providing a "music-app" feel rather than a "video-player" feel.

### 2. Glassmorphism Design
- **Experience**: A modern, translucent UI that adapts to the song's artwork, creating a vibrant and luxorious atmosphere.
- **Technical**: Implemented using advanced CSS `backdrop-filter: blur()` and dynamic HSL color extraction in `player.css`.

### 3. Synchronized Lyrics (Lyrics Sync)
- **Experience**: Lyrics scroll automatically and highlight in real-time as the song plays.
- **Technical**: 
    - JS Controller: `wwwroot/js/app/ui/metadata.js`.
    - Data Source: Fetched via `LyricsService.cs`, parsed from `.lrc` or plain text formats.

### 4. Smart Playback Controls
- **Experience**: Shuffle, Repeat, and variable playback speed (0.5x to 2.0x).
- **Technical**: Orchestrated by `wwwroot/js/app/player/engine.js`. speed is controlled directly via the underlying YouTube IFrame API.

### 5. Picture-in-Picture (PiP)
- **Experience**: Keep the music visualizer and lyrics visible while browsing other tabs.
- **Technical**: Leverages the browser's Document Picture-in-Picture API, enabled in `engine.js`.

---

## 🛠️ Implementation Mapping

| Feature | Key File | Language |
| :--- | :--- | :--- |
| Audio Scraper | `YoutubeStreamResolver.cs` | C# |
| UI Styling | `player.css` / `site.css` | CSS |
| Lyrics Display | `metadata.js` | JS |
| Player Engine | `engine.js` | JS |
| Navigation Flow| `spa-router.js` | JS |
