# 🌟 Pillar 2: Khám phá & Gợi ý Thông minh (Smart Discovery)

VibeMusic makes finding new music effortless by integrating multiple data sources and personalizing the experience from the moment you log in.

---

## ✨ Features

### 1. Personalized Time-of-Day Greetings
- **Experience**: The homepage greets you with "Chào buổi sáng", "Chào buổi chiều", or "Chào buổi tối" based on your local time.
- **Technical**: Logic resides in `HomeFacade.cs`. It calculates the current hour and returns a dynamic greeting string combined with the user's display name.

### 2. Universal "Ultimate" Search
- **Experience**: A single search bar that queries YouTube, internal Song database, Deezer, and iTunes simultaneously.
- **Technical**: Orchestrated in `SearchController.cs` (or `HomeFacade`). It spawns parallel tasks to query external APIs and aggregates the results into a unified DTO.

### 3. Mood-Based Navigation (Moods)
- **Experience**: Floating "Mood" tags (Chill, Focus, Party, Sad) that instantly filter content to match your current vibe.
- **Technical**: Uses `CategoryService.cs` to map songs/playlists to specific mood tags stored in the database.

### 4. Verified Artist Profiles
- **Experience**: Prominent artists feature a "Verified" badge and official discography pages.
- **Technical**: Managed by `ArtistVerificationService.cs` which tracks verification status and badge assets.

### 5. High-Availability Fallback
- **Experience**: If external APIs (YouTube/Deezer) are slow or failing, the app falls back to cached "Discovery" content.
- **Technical**: Implemented in `HomeFacade.cs` using a `try-catch-fallback` pattern.

---

## 🛠️ Implementation Mapping

| Feature | Key File | Layer |
| :--- | :--- | :--- |
| Greeting Logic | `HomeFacade.cs` | Application |
| Search Aggregator | `SearchFacade.cs` / `SearchController.cs` | Application |
| Recommendation Engine| `RecommendationService.cs` | Application |
| Mood Filtering | `CategoryService.cs` | Application |
| Artist Badges | `ArtistVerificationService.cs` | Application |
