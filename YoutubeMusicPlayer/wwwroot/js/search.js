/**
 * search.js - Global search engine for songs and albums
 */

let searchDebounce;
const searchInput = document.getElementById('globalSearchInput');
const searchDropdown = document.getElementById('searchDropdown');
const searchBtn = document.getElementById('globalSearchBtn');

if (searchInput) {
    searchInput.addEventListener('input', (e) => {
        const query = e.target.value.trim();
        clearTimeout(searchDebounce);
        if (query.length < 2) {
            if (searchDropdown) searchDropdown.classList.remove('show');
            return;
        }

        searchDebounce = setTimeout(async () => {
            try {
                const res = await fetch(`/Home/Search?query=${encodeURIComponent(query)}`);
                const data = await res.json();
                if (data && data.length > 0) {
                    let html = '';
                    const hasAlbums = data.some(i => i.type === 'Album');
                    const hasArtists = data.some(i => i.type === 'Artist');
                    const hasSongs = data.some(i => i.type === 'Song');

                    if (hasArtists) {
                        html += '<div class="search-section-header">Nghệ sĩ</div>';
                        html += data.filter(i => i.type === 'Artist').map(item => `
                            <div class="search-item" onclick="window.location.href='/Artist/Details/${item.artistId}'">
                                <img src="${item.thumbnail}" class="rounded-circle" style="aspect-ratio: 1/1; object-fit: cover;" alt="">
                                <div class="search-item-info">
                                    <div class="search-item-title">${item.title} ${item.isVerified ? '<i class="bi bi-patch-check-fill text-accent ms-1"></i>' : ''}</div>
                                    <div class="search-item-author">Nghệ sĩ</div>
                                </div>
                            </div>
                        `).join('');
                    }

                    if (hasAlbums) {
                        html += '<div class="search-section-header">Albums</div>';
                        html += data.filter(i => i.type === 'Album').map(item => `
                            <div class="search-item" onclick="window.location.href='/Album/Details/${item.albumId}'">
                                <img src="${item.thumbnail || '/images/default-album.png'}" alt="">
                                <div class="search-item-info">
                                    <div class="search-item-title">${item.title}</div>
                                    <div class="search-item-author">Nghệ sĩ</div>
                                </div>
                            </div>
                        `).join('');
                    }

                    if (hasSongs) {
                        html += '<div class="search-section-header">Bài hát</div>';
                        html += data.filter(i => i.type === 'Song').map(item => `
                            <div class="search-item" onclick="playSingleTrackFromSearch('${item.videoId}', '${item.title.replace(/'/g, "\\'")}', '${item.author.replace(/'/g, "\\'")}', '${item.thumbnail}')">
                                <img src="${item.thumbnail}" alt="">
                                <div class="search-item-info">
                                    <div class="search-item-title">${item.title}</div>
                                    <div class="search-item-author">${item.author}</div>
                                </div>
                            </div>
                        `).join('');
                    }
                    
                    html += `
                        <div class="search-dropdown-footer border-top border-white border-opacity-10 mt-2 pt-2 text-center">
                            <button class="btn btn-link btn-sm text-accent text-decoration-none fw-bold w-100 py-2" onclick="performFullSearch()">
                                Xem tất cả kết quả cho "${query.replace(/'/g, "\\'")}"
                            </button>
                        </div>
                    `;
                    
                    if (searchDropdown) {
                        searchDropdown.innerHTML = html;
                        searchDropdown.classList.add('show');
                    }
                } else {
                    if (searchDropdown) {
                        searchDropdown.innerHTML = '<div class="p-4 text-center text-dim small">Không tìm thấy kết quả nào.</div>';
                        searchDropdown.classList.add('show');
                    }
                }
            } catch(e) { console.error('Search error:', e); }
        }, 400);
    });

    searchInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') performFullSearch();
    });
}

if (searchBtn) {
    searchBtn.onclick = performFullSearch;
}

window.performFullSearch = async function() {
    const query = searchInput.value.trim();
    if (!query) return;

    if (searchDropdown) searchDropdown.classList.remove('show');
    
    // UI Toggle for search results container
    const homeSections = document.getElementById('home-dynamic-sections');
    const searchMain = document.getElementById('search-results-main');
    const searchContent = document.getElementById('search-results-content');
    const queryTitle = document.getElementById('search-query-title');
    
    if (searchMain && searchContent) {
        // Hide Home components
        document.querySelector('.greeting-text')?.parentElement.classList.add('d-none');
        document.querySelector('.recent-grid')?.parentElement.classList.add('d-none');
        if (homeSections) homeSections.style.display = 'none';
        document.querySelector('.section-reveal:last-of-type')?.classList.add('d-none');

        // Show Search Main
        searchMain.style.display = 'block';
        queryTitle.innerText = `Kết quả cho "${query}"`;
        searchContent.innerHTML = '<div class="text-center p-5"><div class="spinner-border text-accent"></div></div>';

        try {
            const res = await fetch(`/Home/Search?query=${encodeURIComponent(query)}`);
            const data = await res.json();
            if (data && data.length > 0) {
                searchContent.innerHTML = data.map(item => {
                    let onclick = '';
                    let meta = '';
                    let imgClass = 'song-list-img';
                    
                    if (item.type === 'Album') {
                        onclick = `window.location.href='/Album/Details/${item.albumId}'`;
                        meta = 'Album';
                    } else if (item.type === 'Artist') {
                        onclick = `window.location.href='/Artist/Details/${item.artistId}'`;
                        meta = 'Nghệ sĩ';
                        imgClass += ' rounded-circle';
                    } else {
                        onclick = `playSingleTrack('${item.videoId}', '${item.title.replace(/'/g, "\\'")}', '${item.author.replace(/'/g, "\\'")}', '${item.thumbnail}')`;
                        meta = item.author;
                    }

                    return `
                        <div class="song-list-item" onclick="${onclick}">
                            <img src="${item.thumbnail}" class="${imgClass}" style="aspect-ratio: 1/1; object-fit: cover;" alt="">
                            <div class="song-list-info">
                                <div class="song-list-title">${item.title} ${item.type === 'Artist' && item.isVerified ? '<i class="bi bi-patch-check-fill text-accent ml-1"></i>' : ''}</div>
                                <div class="song-list-meta">${meta}</div>
                            </div>
                        </div>
                    `;
                }).join('');
            } else {
                searchContent.innerHTML = '<div class="p-5 text-center text-dim">Không tìm thấy kết quả nào phù hợp.</div>';
            }
        } catch (err) {
            searchContent.innerHTML = '<div class="p-5 text-center text-danger">Đã có lỗi xảy ra khi tìm kiếm.</div>';
        }
    } else {
        // Fallback to Discovery page if we are not on Homepage
        window.location.href = `/Home/Discovery?tag=${encodeURIComponent(query)}`;
    }
}

window.exitSearchMode = function() {
    const searchMain = document.getElementById('search-results-main');
    if (searchMain) {
        searchMain.style.display = 'none';
        document.querySelector('.greeting-text')?.parentElement.classList.remove('d-none');
        document.querySelector('.recent-grid')?.parentElement.classList.remove('d-none');
        const ds = document.getElementById('home-dynamic-sections');
        if (ds) ds.style.display = 'block';
        document.querySelector('.section-reveal:last-of-type')?.classList.remove('d-none');
        if (searchInput) searchInput.value = '';
    }
};

window.playSingleTrackFromSearch = function(vId, title, author, thumb) {
    if (searchDropdown) searchDropdown.classList.remove('show');
    if (window.playSingleTrack) {
        window.playSingleTrack(vId, title, author, thumb);
    }
};
