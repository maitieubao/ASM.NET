# 📚 Pillar 3: Quản lý Thư viện Cá nhân (Library & Social)

VibeMusic provides a seamless environment for users to build and manage their personal music collections.

---

## ✨ Features

### 1. Advanced Playlist Management
- **Experience**: Create private or public playlists, add songs with one click, and reorder tracks.
- **Technical**: Handled by `PlaylistService.cs`. Uses a Many-to-Many relationship between `Playlist` and `Song` entities in PostgreSQL.

### 2. Favorites & Following
- **Experience**: "Like" songs or follow artists to have them appear in your personalized library section.
- **Technical**: Tracked in `InteractionService.cs`. These interactions feed into the recommendation engine.

### 3. Listening History
- **Experience**: Never lose a song again. Your recently played tracks are saved and synced across devices.
- **Technical**: Managed by `InteractionService.cs`. Each play event is recorded with a timestamp and user association.

### 4. Seamless Google Authentication
- **Experience**: One-tap login using your existing Google account for a secure and fast onboarding.
- **Technical**: Implemented using `Microsoft.AspNetCore.Authentication.Google` middleware, configured in `Program.cs`.

---

## 🛠️ Implementation Mapping

| Feature | Key File | Layer |
| :--- | :--- | :--- |
| Playlist CRUD | `PlaylistService.cs` | Application |
| Social Interactions | `InteractionService.cs` | Application |
| Google Auth Config | `Program.cs` / `AuthService.cs` | Web / Application |
| Data Persistence | `AppDbContext.cs` | Infrastructure |
