# Feature: Metadata Bridging & Data Resilience

One of the most complex parts of VibeMusic is the ability to harmonize data between multiple external providers (YouTube, Deezer) and the internal PostgreSQL database.

## 🌉 Metadata Bridging

Traditional music apps often suffer from "Generic Metadata" (e.g., Artist: "Nghệ sĩ", Genre: "Music") when importing from YouTube. VibeMusic solves this using a multi-layered fallback chain.

### The Fallback Chain:
1.  **Level 1: Database (Cache)**: High-speed retrieval of existing saved metadata.
2.  **Level 2: Deezer API (Canonical)**: Used to fetch High-Resolution covers and official artist names via ISRC or Title/Artist matches.
3.  **Level 3: YouTube Search (Fallback)**: If Level 1 & 2 are generic, we extract real-time data from the YouTube video description and channel metadata.

**Logic implemented in**: `PlaybackFacade.AssembleStreamDtoAsync`

## 🏥 Auto-Healing Database (Sanity Checks)

To prevent YouTube's massive view counts (e.g., 170M) from leaking into the system's internal play counts, we use an defensive "Auto-Healing" mechanism.

### Rules of Healing:
- **Threshold**: Any `PlayCount` > 1,000,000 is automatically flagged.
- **Trigger**: Healing happens "On-Access" during Home page sync or Modal detail fetch.
- **Repair**: The record is reset to `0` (or `1`) and the database is updated immediately.

**Logic implemented in**: `HomeFacade.SyncPlayCountsAsync` and `HomeFacade.GetSongPlayCountAsync`.

## ⚡ Parallel Performance Pattern

Loading the Home Page requires data from 5+ external sources and 10+ Internal queries. To avoid timing out, we use **Parallel Isolated Scopes**.

- **Implementation**: Instead of one long `await`, we use `Task.Run`.
- **Concurrency Fix**: Because EF Core's `DbContext` is not thread-safe, each `Task.Run` creates its own `IServiceScope` via `_scopeFactory`. This prevents `ObjectDisposedException`.

```csharp
var trendingTask = Task.Run(async () => {
    using var scope = _scopeFactory.CreateScope();
    var facade = scope.ServiceProvider.GetRequiredService<IHomeFacade>();
    return await facade.GetTrendingSectionAsync();
});
```
