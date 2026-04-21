# 🌊 VibeMusic System Sequence Diagrams

This document visualizes the internal logic of critical system operations. These diagrams explain the coordination between the Frontend, the C# Application Layer, and External Providers.

---

## 1. Luồng Giải quyết Phát nhạc (Playback Resolution)
This flow handles searching, metadata matching, and audio stream extraction within a strict 1.5s timeout.

```mermaid
sequenceDiagram
    participant User as 👤 User
    participant JS as 🌐 Chrome (Player Engine)
    participant Facade as 🏛️ PlaybackFacade
    participant Cache as 🧠 MemoryCache
    participant YT as 📺 YouTube Service
    participant DB as 🗄️ PostgreSQL

    User->>JS: Click Play (Title/Artist)
    JS->>Facade: ResolveAndGetStreamAsync(title, artist)
    
    Facade->>Cache: Check for cached Stream URL
    alt Cache HIT
        Cache-->>Facade: Stream URL
        Facade-->>JS: Playback Stream DTO
    else Cache MISS
        par Parallel Execution
            Facade->>YT: SearchVideosAsync()
            Facade->>DB: GetOrCreate Song (Isolated Scope)
            Facade->>YT: GetAudioStreamUrlAsync()
        end
        
        Note over Facade: Wait Max 1.5s for DB
        
        Facade->>Cache: Save Stream URL & Mapping
        Facade-->>JS: Playback Stream DTO
    end
    
    JS->>User: Start Audio Playback
```

---

## 2. Luồng Tác vụ ngầm (Background Queue)
How VibeMusic offloads heavy tasks (History, Notifications) to keep the UI responsive.

```mermaid
sequenceDiagram
    participant Ctrl as 🎮 Controller / Facade
    participant Queue as 📥 IBackgroundQueue
    participant Host as ⚙️ QueuedHostedService
    participant Work as 🛠️ Background Task
    participant DB as 🗄️ PostgreSQL

    Ctrl->>Queue: QueueBackgroundWorkItemAsync(Func)
    Queue-->>Ctrl: Task Accepted (OK)
    
    Note right of Host: Running in Loop
    Host->>Queue: DequeueAsync()
    Queue-->>Host: WorkItem (Func)
    
    Host->>Work: Invoke(ServiceProvider, Token)
    Work->>DB: Execute logic (e.g. Save History)
    DB-->>Work: Success
    Note over Host: Wait for next task...
```

---

## 3. Luồng Thanh toán Premium (PayOS Lifecycle)
The secure path from upgrading to automatic role assignment.

```mermaid
sequenceDiagram
    participant User as 👤 User
    participant App as 🎵 VibeMusic App
    participant PayOS as 💰 PayOS Gateway
    participant DB as 🗄️ PostgreSQL

    User->>App: Click 'Upgrade to Premium'
    App->>PayOS: CreatePaymentLinkAsync(OrderDetails)
    PayOS-->>App: Checkout URL (VietQR)
    App-->>User: Redirect to Payment Page
    
    User->>PayOS: Pay via Banking App
    PayOS-->>User: Payment Successful!
    
    Note over PayOS, App: Asynchronous Callback
    PayOS->>App: POST Webhook (Signature verified)
    App->>DB: Create Transaction & Set User.IsPremium = true
    App-->>PayOS: HTTP 200 (Acknowledgement)
```

---

## 4. Luồng Tự động làm giàu dữ liệu (Auto-Saving & Enrichment)
Bridging sparse YouTube data to high-quality music metadata.

```mermaid
sequenceDiagram
    participant Svc as 📂 SongService
    participant Queue as 📥 BackgroundQueue
    participant Enrich as 🧪 EnrichmentService
    participant Deezer as 🎵 Deezer/iTunes API
    participant DB as 🗄️ PostgreSQL

    Svc->>DB: Save Basic Song (YoutubeId)
    Svc->>Queue: Queue Enrichment Task
    
    Note over Enrich: Background Worker Starts
    Enrich->>Deezer: SearchTrackAsync(SongTitle, Artist)
    Deezer-->>Enrich: HQ Album Art, Official Lyrics, Genre
    
    Enrich->>DB: Update Song Entity
    Enrich->>DB: Commit UnitOfWork
    Note over Enrich: Metadata Sync Complete
```
