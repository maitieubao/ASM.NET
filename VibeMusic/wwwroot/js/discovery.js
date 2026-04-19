window.discoveryPage = 1;
window.discoveryTag = '';

window.initDiscovery = function() {
    console.log("[Discovery] Initializing components...");
    const container = document.getElementById('discoveryContainer');
    if (container) {
        window.discoveryPage = parseInt(container.getAttribute('data-current-page')) || 1;
        window.discoveryTag = container.getAttribute('data-tag') || '';
    }
};

// Auto-init for full page load
document.addEventListener('DOMContentLoaded', window.initDiscovery);

window.loadMoreDiscovery = async function() {
    const btn = document.getElementById('loadMoreBtn');
    const spinner = document.getElementById('loadMoreSpinner');
    
    if (!btn || !spinner) return;

    btn.classList.add('d-none');
    spinner.classList.remove('d-none');
    
    window.discoveryPage++;
    
    try {
        const response = await fetch(`/Home/Discovery?tag=${encodeURIComponent(window.discoveryTag)}&page=${window.discoveryPage}&json=true`);
        const data = await response.json();
        
        if (data && data.length > 0) {
            const grid = document.getElementById('discoveryGrid');
            if (grid) {
                data.forEach(item => {
                    const card = createDiscoveryCard(item);
                    grid.appendChild(card);
                });
            }
            
            if (data.length < 25) {
                btn.remove(); // No more results
            } else {
                btn.classList.remove('d-none');
            }
        } else {
            btn.remove();
        }
    } catch (err) {
        console.error('Error loading more:', err);
        btn.classList.remove('d-none');
    } finally {
        spinner.classList.add('d-none');
    }
}

function createDiscoveryCard(item) {
    const div = document.createElement('div');
    div.className = 'ytm-card-discovery animate-fade-in';
    
    // Proper string escaping for the onclick attribute
    const vId = item.youtubeVideoId;
    const title = item.title.replace(/'/g, "\\'");
    const author = item.authorName.replace(/'/g, "\\'");
    const thumb = item.thumbnailUrl;

    div.onclick = () => {
        if (window.playSingleTrack) {
            window.playSingleTrack(vId, title, author, thumb);
        }
    };
    
    div.innerHTML = `
        <div class="ytm-card-thumb">
            <img src="${item.thumbnailUrl}" alt="${item.title}" loading="lazy">
            <div class="ytm-card-play"><i class="fa-solid fa-play"></i></div>
        </div>
        <div class="ytm-card-info">
            <div class="ytm-card-title">${item.title}</div>
            <div class="ytm-card-meta">${item.authorName}</div>
        </div>
    `;
    return div;
}

window.playAllDiscovery = function() {
    const cards = document.querySelectorAll('.ytm-card-discovery');
    if (cards.length === 0) return;
    
    const tracks = Array.from(cards).map(card => {
        // We'll extract properties from data attributes if we set them,
        // otherwise we parse the onclick or just read the DOM.
        // Better: let's add data attributes in the Razor view!
        const vId = card.getAttribute('data-video-id');
        const title = card.getAttribute('data-title');
        const author = card.getAttribute('data-author');
        const thumb = card.getAttribute('data-thumbnail');
        
        return {
            videoId: vId,
            title: title || card.querySelector('.ytm-card-title').textContent,
            author: author || card.querySelector('.ytm-card-meta').textContent,
            thumbnail: thumb || card.querySelector('img').src
        };
    });

    if (tracks.length > 0) {
        window.playQueue = tracks;
        window.currentIndex = 0;
        if (window.loadAndPlay) {
            window.loadAndPlay(tracks[0]);
        }
        if (window.renderQueue) {
            window.renderQueue();
        }
    }
}
