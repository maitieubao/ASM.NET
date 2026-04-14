/**
 * search.js - Global search engine for songs and albums
 */

let searchDebounce;
const searchInput = document.getElementById('globalSearchInput');
const searchDropdown = document.getElementById('searchDropdown');
const searchBtn = document.getElementById('globalSearchBtn');

window.initSearchUI = function() {
    console.log("[Search] Initializing UI...");
    const searchInput = document.getElementById('globalSearchInput');
    const searchBtn = document.getElementById('globalSearchBtn');
    
    if (searchInput) {
        // Clear previous listeners to avoid duplicates
        const newInput = searchInput.cloneNode(true);
        searchInput.parentNode.replaceChild(newInput, searchInput);
        
        newInput.onkeypress = (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                performFullSearch();
            }
        };
    }

    if (searchBtn) {
        searchBtn.onclick = (e) => {
            if (e) e.preventDefault();
            performFullSearch();
        };
    }
    
    const searchIcon = document.querySelector('.search-input-wrapper i.fa-magnifying-glass');
    if (searchIcon) {
        searchIcon.style.cursor = 'pointer';
        searchIcon.onclick = (e) => {
            if (e) e.preventDefault();
            performFullSearch();
        };
    }
};

window.performFullSearch = async function() {
    const searchInput = document.getElementById('globalSearchInput');
    const query = searchInput?.value.trim();
    if (!query) return;

    if (searchDropdown) searchDropdown.classList.remove('show');
    
    const homeSections = document.getElementById('home-dynamic-sections');
    const searchMain = document.getElementById('search-results-main');
    const searchContent = document.getElementById('search-results-content');
    const queryTitle = document.getElementById('search-query-title');
    
    if (searchMain && searchContent) {
        // Hide standard Home page elements
        const greetingParent = document.querySelector('.greeting-text')?.parentElement;
        const recentParent = document.querySelector('.recent-grid')?.parentElement;
        const artistSection = document.querySelector('.section-reveal:not(#search-results-main)');

        if (greetingParent) greetingParent.classList.add('d-none');
        if (recentParent) recentParent.classList.add('d-none');
        if (homeSections) homeSections.style.display = 'none';
        if (artistSection) artistSection.classList.add('d-none');

        // Show Search View
        searchMain.style.display = 'block';
        searchMain.classList.add('active'); // Trigger animation
        if (queryTitle) queryTitle.innerText = `Tìm kiếm cho "${query}"`;
        searchContent.className = "search-results-list"; // Vertical list
        searchContent.innerHTML = `
            <div class="text-center p-5">
                <div class="spinner-border text-accent mb-3" style="width: 3rem; height: 3rem;"></div>
                <div class="text-dim">Đang tìm kiếm giai điệu của bạn...</div>
            </div>
        `;

        try {
            const res = await fetch(`/Home/Search?query=${encodeURIComponent(query)}`);
            const data = await res.json();
            
            if (data && data.length > 0) {
                const trackList = data.filter(i => i.type === 'Song').map(s => ({
                    videoId: s.videoId,
                    title: s.title,
                    author: s.author,
                    thumbnail: s.thumbnail
                }));
                
                searchContent.innerHTML = data.map((item, idx) => {
                    let typeLabel = '';
                    let onclick = '';
                    let itemClass = 'search-result-row';
                    let imgClass = 'search-result-img';
                    
                    if (item.type === 'Album') {
                        const url = item.source === 'Internal' ? `/Album/Details/${item.albumId}` : `/Album/DetailsEx?source=${item.source}&id=${item.externalId}`;
                        onclick = `(window.SPARouter ? window.SPARouter.navigateTo('${url}') : window.location.href='${url}')`;
                        typeLabel = 'Album';
                    } else if (item.type === 'Artist') {
                        const url = `/Artist/Details/${item.artistId}`;
                        onclick = `(window.SPARouter ? window.SPARouter.navigateTo('${url}') : window.location.href='${url}')`;
                        typeLabel = 'Nghệ sĩ';
                        itemClass += ' is-artist';
                        imgClass += ' rounded-circle';
                    } else {
                        const songIdx = trackList.findIndex(s => s.videoId === item.videoId);
                        onclick = `playTrackInContext(${JSON.stringify(trackList).replace(/"/g, '&quot;')}, ${songIdx})`;
                        typeLabel = item.author || 'Bài hát';
                    }

                    return `
                        <div class="${itemClass}" onclick="${onclick}">
                            <div class="row-thumbnail">
                                <img src="${item.thumbnail}" class="${imgClass}" alt="">
                                ${item.type === 'Song' ? '<div class="row-play-overlay"><i class="fa-solid fa-play"></i></div>' : ''}
                            </div>
                            <div class="row-content">
                                <div class="row-title">
                                    ${item.title} 
                                    ${item.type === 'Artist' && item.isVerified ? '<i class="bi bi-patch-check-fill text-accent ms-1"></i>' : ''}
                                </div>
                                <div class="row-meta">
                                    ${item.type} • ${typeLabel}
                                </div>
                            </div>
                            <div class="row-actions">
                                <button class="btn-icon-subtle" title="Yêu thích" onclick="event.stopPropagation(); togglePlayerLike()">
                                    <i class="fa-regular fa-heart"></i>
                                </button>
                                <button class="btn-icon-subtle" title="Thêm vào Playlist" onclick="event.stopPropagation(); showAddToPlaylistModal('${item.videoId}')">
                                    <i class="fa-solid fa-folder-plus text-accent"></i>
                                </button>
                                <button class="btn-icon-subtle" title="Khác">
                                    <i class="fa-solid fa-ellipsis-vertical"></i>
                                </button>
                            </div>
                        </div>
                    `;
                }).join('');
            } else {
                searchContent.innerHTML = `
                    <div id="no-search-results" class="col-12 p-5 text-center">
                        <i class="fa-solid fa-magnifying-glass-chart fs-1 text-dim mb-3"></i>
                        <h4 class="text-white">Không tìm thấy kết quả</h4>
                        <p class="text-dim">Hãy thử với từ khóa khác như tên bài hát hoặc nghệ sĩ.</p>
                    </div>
                `;
            }
        } catch (err) {
            console.error("Search failed:", err);
            searchContent.innerHTML = '<div id="search-error" class="col-12 p-5 text-center text-danger">Có lỗi xảy ra khi tìm kiếm bài hát. Vui lòng thử lại.</div>';
        }
    } else {
        const url = `/Home/Discovery?tag=${encodeURIComponent(query)}`;
        if (window.SPARouter) window.SPARouter.navigateTo(url);
        else window.location.href = url;
    }
};

window.exitSearchMode = function() {
    const searchMain = document.getElementById('search-results-main');
    if (searchMain) {
        searchMain.style.display = 'none';
        
        const greetingParent = document.querySelector('.greeting-text')?.parentElement;
        const recentParent = document.querySelector('.recent-grid')?.parentElement;
        const homeSections = document.getElementById('home-dynamic-sections');
        const artistSection = document.querySelector('.section-reveal:not(#search-results-main)');

        if (greetingParent) greetingParent.classList.remove('d-none');
        if (recentParent) recentParent.classList.remove('d-none');
        if (homeSections) homeSections.style.display = 'block';
        if (artistSection) artistSection.classList.remove('d-none');
        
        const searchInput = document.getElementById('globalSearchInput');
        if (searchInput) searchInput.value = '';
    }
};

// Auto-boot
$(function() {
    window.initSearchUI();
});
