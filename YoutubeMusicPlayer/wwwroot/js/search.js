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
            return;
        }

        searchDebounce = setTimeout(async () => {
             // We no longer show dropdown results automatically.
             // Users must press Enter or click search button.
        }, 400);
    });

    searchInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') performFullSearch();
    });
}

if (searchBtn) {
    searchBtn.onclick = (e) => {
        if (e) e.preventDefault();
        performFullSearch();
    };
}

// Ensure Enter key works on input (overwriting previous keypress if exists)
if (searchInput) {
    searchInput.onkeypress = (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            performFullSearch();
        }
    };
}

// Also bind to the magnifying glass icon inside the wrapper
const searchIcon = document.querySelector('.search-input-wrapper i.fa-magnifying-glass');
if (searchIcon) {
    searchIcon.style.cursor = 'pointer';
    searchIcon.onclick = (e) => {
        if (e) e.preventDefault();
        performFullSearch();
    };
}

window.performFullSearch = async function() {
    const query = searchInput.value.trim();
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
        queryTitle.innerText = `Tìm kiếm cho "${query}"`;
        searchContent.className = "row row-cols-1 row-cols-md-2 row-cols-xl-3 g-3"; // Restore grid layout
        searchContent.innerHTML = `
            <div class="col-12 text-center p-5">
                <div class="spinner-border text-accent mb-3" style="width: 3rem; height: 3rem;"></div>
                <div class="text-dim">Đang tìm kiếm giai điệu của bạn...</div>
            </div>
        `;

        try {
            const res = await fetch(`/Home/Search?query=${encodeURIComponent(query)}`);
            const data = await res.json();
            
            if (data && data.length > 0) {
                searchContent.innerHTML = data.map(item => {
                    let typeLabel = '';
                    let onclick = '';
                    let itemClass = 'song-list-item search-result-item h-100';
                    let imgStyle = 'width: 56px; height: 56px;';
                    let imgClass = 'song-list-img';
                    
                    if (item.type === 'Album') {
                        onclick = `window.location.href='/Album/Details/${item.albumId}'`;
                        typeLabel = '<span class="tag-badge py-0 px-2 extra-small opacity-75">Album</span>';
                    } else if (item.type === 'Artist') {
                        onclick = `window.location.href='/Artist/Details/${item.artistId}'`;
                        typeLabel = '<span class="tag-badge bg-accent text-white py-0 px-2 extra-small">Nghệ sĩ</span>';
                        itemClass += ' search-result-artist';
                        imgStyle = 'width: 64px; height: 64px;';
                    } else {
                        onclick = `playSingleTrack('${item.videoId}', '${item.title.replace(/'/g, "\\'")}', '${item.author.replace(/'/g, "\\'")}', '${item.thumbnail}')`;
                        typeLabel = `<span class="text-dim extra-small">${item.author}</span>`;
                    }

                    return `
                        <div class="col">
                            <div class="${itemClass}" onclick="${onclick}">
                                <div class="d-flex align-items-center flex-grow-1 min-w-0">
                                    <div class="position-relative flex-shrink-0 me-3">
                                        <img src="${item.thumbnail}" class="${imgClass}" style="${imgStyle}" alt="">
                                        ${item.type === 'Song' ? '<div class="song-list-play-overlay"><i class="fa-solid fa-play"></i></div>' : ''}
                                    </div>
                                    <div class="song-list-info overflow-hidden">
                                        <div class="song-list-title fs-6 fw-bold mb-1 text-truncate" title="${item.title}">
                                            ${item.title} 
                                            ${item.type === 'Artist' && item.isVerified ? '<i class="bi bi-patch-check-fill text-accent ms-1"></i>' : ''}
                                        </div>
                                        <div class="song-list-meta d-flex align-items-center gap-2">
                                            ${typeLabel}
                                        </div>
                                    </div>
                                </div>
                                <div class="ms-auto flex-shrink-0 d-flex align-items-center gap-3">
                                    <i class="fa-solid fa-ellipsis-vertical text-dim cursor-pointer p-2 hover-text-main"></i>
                                </div>
                            </div>
                        </div>
                    `;
                }).join('');
            } else {
                searchContent.innerHTML = `
                    <div class="col-12 p-5 text-center">
                        <i class="fa-solid fa-magnifying-glass-chart fs-1 text-dim mb-3"></i>
                        <h4 class="text-white">Không tìm thấy kết quả</h4>
                        <p class="text-dim">Hãy thử với từ khóa khác như tên bài hát hoặc nghệ sĩ.</p>
                    </div>
                `;
            }
        } catch (err) {
            searchContent.innerHTML = '<div class="col-12 p-5 text-center text-danger">Có lỗi xảy ra khi tìm kiếm bài hát. Vui lòng thử lại.</div>';
        }
    } else {
        window.location.href = `/Home/Discovery?tag=${encodeURIComponent(query)}`;
    }
}

window.exitSearchMode = function() {
    const searchMain = document.getElementById('search-results-main');
    if (searchMain) {
        searchMain.style.display = 'none';
        
        // Restore all standard Home page sections
        const greetingParent = document.querySelector('.greeting-text')?.parentElement;
        const recentParent = document.querySelector('.recent-grid')?.parentElement;
        const homeSections = document.getElementById('home-dynamic-sections');
        const artistSection = document.querySelector('.section-reveal:not(#search-results-main)');

        if (greetingParent) greetingParent.classList.remove('d-none');
        if (recentParent) recentParent.classList.remove('d-none');
        if (homeSections) homeSections.style.display = 'block';
        if (artistSection) artistSection.classList.remove('d-none');
        
        if (searchInput) searchInput.value = '';
    }
};
