/**
 * state.js - Centralized player state
 */
window.playQueue = [];
window.currentIndex = -1;
window.isShuffle = false;
window.repeatMode = 0; // 0: None, 1: One, 2: All
window.adSequenceActive = false;
window.streamCache = {};
window.currentSongDbId = null;
window.lastReportedTime = 0;
window.trackingInterval = null;
window.isDraggingProgress = false;
window.isSongLoading = false; 
window.consecutiveErrorCount = 0; 

// References to shared UI elements
window.audioPlayer = null;
window.playPauseBtn = null;
window.progressSlider = null;
window.currentTimeEl = null;
window.durationTimeEl = null;
