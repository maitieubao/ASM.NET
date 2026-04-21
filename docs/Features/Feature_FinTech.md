# 💎 Pillar 4: Tính năng Premium & Thanh toán (FinTech)

VibeMusic integrates modern payment solutions to provide a premium, uninterrupted music experience.

---

## ✨ Features

### 1. PayOS & VietQR Integration
- **Experience**: Upgrade to Premium instantly using VietQR. The system automatically detects payment and unlocks features.
- **Technical**: 
    - Service: `PayOSService.cs`.
    - Logic: Generates unique PayOS checkout links and handles Webhook notifications to update the user's `Subscription` status in the DB.

### 2. High-Quality MP4 Downloads
- **Experience**: Premium users can download any song directly as high-bitrate MP4 audio for offline listening.
- **Technical**: 
    - Frontend Trigger: `handlePremiumDownload` in `wwwroot/js/site.js`.
    - Backend: `SubscriptionController` verification logic ensuring only users with active `Premium` status can access the download stream.

### 3. Subscription Lifecycle Management
- **Experience**: Three tiers (1 Month, 1 Year, Lifetime) with automatic expiry notifications.
- **Technical**: Managed by `SubscriptionService.cs`. It calculates expiry dates and manages "Lifetime" flags for the account.

---

## 🛠️ Implementation Mapping

| Feature | Key File | Layer |
| :--- | :--- | :--- |
| Payment Gateway | `PayOSService.cs` | Infrastructure |
| Billing Logic | `SubscriptionService.cs` | Application |
| Download Proxy | `SongController.cs` | Web |
| UI Triggers | `site.js` | Web (JS) |
