# 📘 VibeMusic Full Technical Catalog

This document is an exhaustive registry of every service, plugin, and utility file in the VibeMusic project. It serves as a single source of truth for the system's technical capabilities.

---

## 🏛️ Application Layer: Services (`VibeMusic.Application/Services`)

These services implement the core business logic and interface with the Domain models.

| File | Responsibility | Key Features |
| :--- | :--- | :--- |
| **AlbumService.cs** | Manage Album catalog | CRUD for albums, association with artists and songs. |
| **ArtistService.cs** | Manage Artist metadata | Artist profiles, top tracks, and discography links. |
| **ArtistVerification...** | Verification Workflow| Handles application status (Pending/Verified) and badges. |
| **AuthService.cs** | Identity & Security | User registration, login logic, and JWT/Cookie issuance. |
| **BackgroundQueue.cs** | Async Task Producer | Thread-safe `Channel<T>` for queuing background work. |
| **CacheWarmupService** | Startup Optimization | Pre-populates Redis/MemoryCache with trending data. |
| **CategoryService.cs** | Content Grouping | Logical grouping of songs into moods, eras, or types. |
| **CommentService.cs** | Social Engagement | CRUD for comments, threaded replies, and likes. |
| **DashboardService.cs**| Admin Analytics | Aggregates stats for the admin management panel. |
| **ExtViewCountSync...** | External Metrics | (Legacy) Syncs YouTube/Deezer stats into internal tables. |
| **GenreService.cs** | Catalog Taxonomy | Management of music genres and related song filtering. |
| **InteractiveService** | User Behavior | Tracks play history, likes, and follows. |
| **LyricsService.cs** | Lyric Retrieval | Sources lyrics from external providers (LRC formats). |
| **NotificationService**| User Alerts | Dispatches system and social alerts to users. |
| **PayOSService.cs** | Payment Orchestrator| Handles checkout links and webhook validation. |
| **PlaylistService.cs** | Library Management | User-created and Smart dynamic playlists. |
| **QueuedHosted...** | Background Consumer | Background worker that executes items from `BackgroundQueue`. |
| **RecommendationSvc** | Discovery Engine | Algorithmic song suggestions based on history. |
| **SongMetadataEnrich**| Metadata Repair | Background task to fetch high-res art/tags for imports. |
| **SongService.cs** | Core Music CRUD | Main repository wrapper for the Song entity. |
| **SubscriptionService**| Billing Logic | Tier management (Month/Year/Lifetime) and expiry tracking. |
| **UserService.cs** | User Profile Mgmt | Handles non-auth user data (Avatars, Bio, Preferences). |
| **ViewCountService** | Logic Play Counting | Real-time increment and verification of internal plays. |

---

## 🔌 Infrastructure Layer: External & AI (`VibeMusic.Infrastructure/External`)

Implementations for third-party integrations and media processing.

### 🤖 AI Intelligence & Semantic Kernel

| File / Folder | Responsibility | Tech Detail |
| :--- | :--- | :--- |
| **SemanticKernelAgent**| AI Orchestrator | Core AI engine using Google Gemini via Semantic Kernel. |
| **AiPlugins/** | Tool-Calling Logic | Custom plugins: `MusicSearch`, `UserInteraction`, `Wikipedia`. |
| **ActionExtractor.cs** | Intent Discovery | Parses LLM outputs into system-executable JSON actions. |
| **KernelFactory.cs** | Kernel Setup | Configures connectors and registers all AI plugins. |
| **PromptBuilder.cs** | System Instructions | Manages the complex "Antigravity AI" system persona. |
| **RetryHandler.cs** | LLM Resilience | Custom HTTP handler for 429 Rate Limit backoff. |

### 🎵 Media Engine & YouTube

| File | Responsibility | Key Features |
| :--- | :--- | :--- |
| **YoutubeService.cs** | Orchestrator | High-level API for seeking and playing YouTube content. |
| **YoutubeApiClient.cs**| Raw API Client | Low-level wrapper for the official YouTube Data API. |
| **YoutubeStreamResol**| Audio Provider | Extracts direct `.m4a`/`.opus` URLs via YoutubeExplode. |
| **TrackMetadataProc** | Data Sanitizer | Cleans messy YT titles and parses Artist/Title tags. |
| **VideoEnrichmentSvc** | Data Augmentation | Cross-references YouTube with Deezer for official data. |
| **DeezerService.cs** | Music Provider | Fetches canonical album art and artist bio from Deezer. |
| **iTunesService.cs** | Music Provider | Backup source for high-quality Apple Music metadata. |
| **MusicContentFilter**| Safety/Duplicate | Filters out restricted content and prevents duplicate imports. |
| **WikipediaService** | Bio/Knowledge | Fetches trivia and artist history from Wikipedia. |

---

## 🛡️ Facades (`VibeMusic.Application/Services`)

High-level entry points that orchestrate multiple services for specific UI pages.

- **HomeFacade.cs**: Orchestrates Trends, Recommendations, and Greetings for the landing page.
- **PlaybackFacade.cs**: Orchestrates Metadata, Stream URL, and Play count incrementing.
- **ProfileFacade.cs**: Orchestrates User history, Likes, and Subscription status for the profile page.
