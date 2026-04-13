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
    
    const bgStyle = `linear-gradient(to bottom, rgba(0,0,0,0.4), #0f0f0f), url(${thumbUrl})`;
    
    $('#dynamicBG').css({
        'background-image': bgStyle,
        'background-size': 'cover',
        'background-position': 'center',
        'filter': 'blur(100px) brightness(0.4)',
        'opacity': '0.5'
    });
    
    $('#fullPlayerBackdrop').css('background-image', `url(${thumbUrl})`);
};

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
