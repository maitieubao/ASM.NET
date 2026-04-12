# YoutubeMusicPlayer - Implementation Documentation & Solution Map

This project is a sophisticated Music Player platform that leverages **YouTube Explode API** for streaming and lyrics discovery, built with **ASP.NET Core 10** following **Clean Architecture**.

---

## 🏛️ Project Architecture
The project adheres to strict Clean Architecture boundaries:
- **Presentation (Web)**: Controllers, UI (Razor/JS/CSS).
- **Application**: Business logic orchestrators (Services, Facades, DTOs).
- **Domain**: Core business rules and entities.
- **Infrastructure**: External integrations and DB persistence.

---

## 📂 Detailed File Catalog

### 💠 [YoutubeMusicPlayer.Domain](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/) (Core)
- **Entities/**:
    - [Album.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/Album.cs) - Album metadata.
    - [Artist.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/Artist.cs) - Artist bio and links.
    - [Song.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/Song.cs) - Song definitions and Lyrics stores.
    - [User.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/User.cs) - User credentials and preferences.
    - [Playlist.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/Playlist.cs), [PlaylistSong.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/PlaylistSong.cs)
    - [Payment.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/Payment.cs), [SubscriptionPlan.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/SubscriptionPlan.cs)
    - [Report.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/Report.cs) - User-generated reports.
    - [SongArtist.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/SongArtist.cs), [SongGenre.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/SongGenre.cs)
    - [UserGenreStat.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Entities/UserGenreStat.cs) - Analytics data.
- **Interfaces/**:
    - [IUnitOfWork.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Interfaces/IUnitOfWork.cs) - Transaction orchestration.
    - [IGenericRepository.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Interfaces/IGenericRepository.cs)
    - [IDbTransaction.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Domain/Interfaces/IDbTransaction.cs)

### ⚙️ [YoutubeMusicPlayer.Application](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Application/) (Orchestration)
- **Interfaces/**:
    - `ISongService`, `IArtistService`, `IAuthService`, `IAlbumService`, `IPlaylistService`, `IPayOSService`.
    - [ILyricsService.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Application/Interfaces/ILyricsService.cs) - Lyrics orchestration interface.
    - `IYoutubeService` - Interface for YouTube SDK interaction.
- **Services/**:
    - [LyricsService.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Application/Services/LyricsService.cs) - Orchestrates multi-video search fallback for lyrics.
    - `SongService.cs` - Core enrichment logic.
    - `BackgroundQueue.cs`, `QueuedHostedService.cs` - Non-blocking enrichment workers.
- **Facades/**:
    - [PlaybackFacade.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Application/Services/PlaybackFacade.cs) - Unified player logic.
    - `HomeFacade.cs`, `ProfileFacade.cs`.
- **DTOs/**:
    - [LyricsResult.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Application/DTOs/LyricsResult.cs) - Response schema for lyrics requests.

### 🌐 [YoutubeMusicPlayer.Infrastructure](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Infrastructure/) (Implementation)
- **External/**:
    - [YoutubeService.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Infrastructure/External/YoutubeService.cs) - **YoutubeExplode** integration.
    - `DeezerService.cs`, `WikipediaService.cs`.
    - `SemanticKernelAgentService.cs` - AI-powered features.
- **External/AiPlugins/**:
    - `MusicSearchPlugin.cs`, `PlaylistPlugin.cs`, `WikipediaPlugin.cs`.
- **Persistence/**:
    - [AppDbContext.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Infrastructure/Persistence/AppDbContext.cs) - EF Core configuration and SQL migration logic.
- **Repositories/**:
    - `GenericRepository.cs`.
- **UnitOfWork.cs** - Concrete transaction handling.

### 🖥️ [YoutubeMusicPlayer](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer/) (Presentation)
- **Controllers/**:
    - `HomeController.cs`, `AuthController.cs`, `SongController.cs`, `AlbumController.cs`, `ArtistController.cs`.
    - `AdminController.cs`, `DashboardController.cs`, `PaymentController.cs`, `InteractionController.cs`.
- **Program.cs** - Dependency Injection (DI) and App middleware configuration.
- **wwwroot/**:
    - `js/ui.js` - UI interaction and lyrics rendering logic.
    - `css/modules/immersive.css` - Glassmorphic player overlay styles.

### 🧪 [YoutubeMusicPlayer.Tests](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Tests/) (Automation)
- **FunctionalTests/Selenium/**:
    - [YoutubeLyricsValidationTests.cs](file:///c:/Users/maiti/OneDrive/Desktop/ASM.NET/YoutubeMusicPlayer.Tests/FunctionalTests/Selenium/YoutubeLyricsValidationTests.cs) - Automated E2E verification of lyrics.
- **UnitTests/**:
    - `SongServiceTests.cs`, `LyricsLogicTests.cs`, `YoutubeServiceTests.cs`.

---

## 🎵 Implementation Specifics: Lyrics Feature
1. **Search**: `LyricsService` cleans track titles and searches for "Lyrics" versions on YouTube if the primary video lacks subtitles.
2. **Extraction**: `YoutubeService` parses Closed Captions (Subtitles) from YouTube using language priority (vi -> en -> auto).
3. **Caching**: Results are cached in `IMemoryCache` and persisted to PostgreSQL for instant future retrieval.
4. **Resiliency**: Real-time fallback ensures that even newly added songs get lyrics immediately.
