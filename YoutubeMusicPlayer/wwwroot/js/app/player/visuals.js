/**
 * visuals.js - Player UI updates and thematic effects (jQuery Refactored)
 */

window.updatePlayPauseUI = function(isPlaying) {
    $('#playPauseBtn').attr('class', isPlaying ? 'fa-solid fa-circle-pause play-main' : 'fa-solid fa-circle-play play-main');
};

window.setPlayerMessage = function(title, author) {
    const $title = $('#currentTitle');
    const $author = $('#currentAuthor');
    $title.text(title || 'Unknown Title');
    $author.text(author || 'Unknown Artist');
    
    if (title && (title.includes('Lỗi') || title.includes('STOP'))) {
        $title.addClass('text-danger fw-bold');
    } else {
        $title.removeClass('text-danger fw-bold');
    }
};

window.updateDynamicTheme = function(thumbUrl) {
    if (!thumbUrl) return;
    
    // 1. Update Blurred Background (Optimized Brightness)
    const bgStyle = `linear-gradient(to bottom, rgba(0,0,0,0.3), #070707), url(${thumbUrl})`;
    
    $('#dynamicBG').css({
        'background-image': bgStyle,
        'background-size': 'cover',
        'background-position': 'center',
        'filter': 'blur(80px) saturate(1.5) brightness(0.8)', // Increased brightness & saturation
        'opacity': '0.6'
    });
    
    $('#fullPlayerBackdrop').css({
        'background-image': `url(${thumbUrl})`,
        'filter': 'blur(100px) saturate(1.8) brightness(0.7)',
        'opacity': '0.5'
    });

    // 2. Extract Dominant Color for Global UI Sync
    extractDominantColor(thumbUrl, (color) => {
        if (!color) return;
        
        // Update Root Variables for Global Consistency
        const root = document.documentElement;
        root.style.setProperty('--accent-primary', color);
        root.style.setProperty('--accent-glow', `${color}80`); // 50% opacity for glow
        
        console.log(`[Visuals] Dynamic Theme Updated: ${color}`);
    });
};

/**
 * Extracts average color from an image URL using a hidden canvas.
 * Handles CORS issues by falling back gracefully.
 */
function extractDominantColor(url, callback) {
    const img = new Image();
    img.crossOrigin = "Anonymous"; 
    img.src = url;
    
    img.onload = function() {
        try {
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            canvas.width = 1;
            canvas.height = 1;
            
            // Draw 1x1 to get average color automatically
            ctx.drawImage(img, 0, 0, 1, 1);
            const data = ctx.getImageData(0, 0, 1, 1).data;
            
            // Adjust color to be more vibrant if it's too dark
            let r = data[0], g = data[1], b = data[2];
            const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            
            if (luminance < 40) { // Too dark, boost it
                r = Math.min(255, r + 50);
                g = Math.min(255, g + 50);
                b = Math.min(255, b + 50);
            }
            
            const hex = "#" + ((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1);
            callback(hex);
        } catch (e) {
            console.warn("[Visuals] Color extraction blocked by CORS or error:", e);
            callback("#6C5CE7"); // Fallback to default brand purple
        }
    };
    
    img.onerror = function() {
        callback("#6C5CE7");
    };
}

window.renderQueue = function() {
    const $container = $('#queueContainer');
    if (!$container.length) return;
    
    if (window.playQueue.length === 0) {
        $container.html('<div class="p-5 text-center text-dim">Danh sách chờ trống.</div>');
        return;
    }
    
    const html = window.playQueue.map((t, i) => `
        <div class="queue-item ${i === window.currentIndex ? 'active' : ''}" onclick="window.currentIndex=${i}; loadAndPlay(window.playQueue[window.currentIndex]); renderQueue();">
            <img src="${t.thumbnail}" class="queue-item-thumb" alt="">
            <div class="queue-item-info">
                <div class="queue-item-title">${t.title}</div>
                <div class="queue-item-artist">${t.author}</div>
            </div>
            ${i === window.currentIndex ? '<div class="playing-bars"><div class="bar"></div><div class="bar"></div><div class="bar"></div></div>' : ''}
            <button class="btn btn-link text-white-50 p-1 ms-2 hover-text-danger" onclick="event.stopPropagation(); removeFromQueue(${i})">
                <i class="fa-solid fa-xmark"></i>
            </button>
        </div>
    `).join('');
    
    $container.html(html);

    const $active = $container.find('.queue-item.active');
    if ($active.length) {
        $active[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
};

window.formatTime = function(s) {
    if (isNaN(s)) return "0:00";
    const m = Math.floor(s / 60);
    const r = Math.floor(s % 60);
    return `${m}:${r < 10 ? '0' : ''}${r}`;
};

window.updateVisualsAndMetadata = function(track, data) {
    // 1. Update Like Button State
    const $icon = $('#playerLikeBtn i');
    if ($icon.length) {
        if (data && data.isLiked) {
            $icon.attr('class', 'fa-solid fa-heart text-danger');
        } else {
            $icon.attr('class', 'fa-regular fa-heart text-dim');
        }
    }
    
    // 2. Update Full Screen Player Elements
    $('#fullPlayerTitle').text(track.title);
    $('#fullPlayerArtist').text(track.author);
    $('#fullPlayerThumb').attr('src', track.thumbnail || 'https://ui-avatars.com/api/?name=Music&background=181818&color=6C5CE7');
    
    // 3. Update Dynamic Theme
    window.updateDynamicTheme(track.thumbnail);
    
    if (typeof fetchRichMetadata === 'function') {
        fetchRichMetadata(track.videoId); 
    }
};
