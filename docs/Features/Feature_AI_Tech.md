# 🤖 Pillar 5: Trợ lý AI & Công nghệ (AI & Tech)

VibeMusic is built on a state-of-the-art tech stack that leverages AI and background processing to deliver a seamless experience.

---

## ✨ Tech Features

### 1. "Antigravity AI" Assistant
- **Experience**: Tell the AI "Phát những bài hát của Ed Sheeran" or "Tạo cho tôi một playlist nhạc Chill", and it will happen automatically.
- **Technical**: 
    - Engine: **Microsoft Semantic Kernel**.
    - Model: **Google Gemini** (via Groq for speed).
    - Logic: `SemanticKernelAgentService.cs` orchestrates plugins like `PlaylistPlugin` and `MusicSearchPlugin`.

### 2. Clean Architecture (CQRS Lite)
- **Experience**: The codebase is modular, making it easy to add new features without breaking existing ones.
- **Technical**: Strictly separated into **Domain**, **Application**, **Infrastructure**, and **Web** layers to prevent tight coupling.

### 3. Background Task Orchestration
- **Experience**: Music data is enriched and repaired (high-res art, tags) in the background without slowing down the UI.
- **Technical**: 
    - Producer: `BackgroundQueue.cs`.
    - Consumer: `QueuedHostedService.cs`.
    - Work: `SongMetadataEnrichmentService.cs`.

### 4. High-Performance Caching
- **Experience**: Trending content loads instantly.
- **Technical**: `CacheWarmupService.cs` runs at startup to pre-calculate expensive home page queries and store them in memory.

---

## 🛠️ Implementation Mapping

| Feature | Key File | Layer |
| :--- | :--- | :--- |
| AI Agent | `SemanticKernelAgentService.cs` | Infrastructure |
| Background Loop | `QueuedHostedService.cs` | Application |
| Work Queue | `BackgroundQueue.cs` | Application |
| Data Enrichment | `SongMetadataEnrichmentService.cs`| Application |
| Startup Optimization| `CacheWarmupService.cs` | Application |
