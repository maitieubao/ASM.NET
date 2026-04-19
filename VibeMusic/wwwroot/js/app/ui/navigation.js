/**
 * navigation.js - Sidebar, drawers, and carousel management (jQuery Refactored)
 */

window.toggleQueue = function() {
    $('#queueDrawer').toggleClass('open');
};

window.toggleMetadata = function() {
    const $md = $('#fullPlayerOverlay');
    if ($md.length) {
        $md.toggleClass('open');
        const isOpen = $md.hasClass('open');
        
        if (isOpen) {
            const thumb = $('#currentThumbnail').attr('src');
            const title = $('#currentTitle').text();
            const artist = $('#currentAuthor').text();
            
            if (thumb) {
                $('#fullPlayerThumb').attr('src', thumb);
                $('#fullPlayerBackdrop').css({
                    'background-image': `url(${thumb})`,
                    'background-size': 'cover',
                    'background-position': 'center',
                    'filter': 'blur(80px) brightness(0.5)'
                });
            }
            if (title) $('#fullPlayerTitle').text(title);
            if (artist) $('#fullPlayerArtist').text(artist);

            $('body').css('overflow', 'hidden');
        } else {
            $('body').css('overflow', '');
        }
    }
};

window.scrollCarousel = function(btn, direction) {
    const $carousel = $(btn).parent().find('.horizontal-carousel');
    if ($carousel.length) {
        const scrollAmount = $carousel.outerWidth() * 0.8;
        $carousel[0].scrollBy({
            left: direction * scrollAmount,
            behavior: 'smooth'
        });
    }
};

window.loadSidebarPlaylists = function() {
    const $container = $('#sidebarPlaylists');
    if (!$container.length || !window.YTM_CONFIG || !window.YTM_CONFIG.isAuthenticated) return;

    $.getJSON('/Playlist/GetPlaylistsJson')
        .done(function(data) {
            const playlists = data.data || data;
            if (playlists && playlists.length > 0) {
                const html = playlists.map(p => `
                    <a href="/Playlist/Details/${p.playlistId}" class="nav-item small py-2 px-1 opacity-75 hover-opacity-100 text-decoration-none d-flex align-items-center">
                        <i class="fa-solid fa-music text-dim me-2"></i>
                        <span class="text-truncate">${p.title}</span>
                    </a>
                `).join('');
                $container.html(html);
            }
        })
        .fail(function(err) {
            console.warn("[Navigation] Failed to load playlists:", err);
        });
};

$(function() {
    window.loadSidebarPlaylists();

    // Responsive Sidebar Toggle
    $('#mobileMenuBtn').on('click', function(e) {
        e.stopPropagation();
        $('#sidebar').toggleClass('show');
    });

    // Close sidebar when clicking outside on mobile
    $(document).on('click', function(e) {
        if ($(window).width() <= 992) {
            const $sidebar = $('#sidebar');
            const $btn = $('#mobileMenuBtn');
            if (!$sidebar.is(e.target) && $sidebar.has(e.target).length === 0 && !$btn.is(e.target) && $btn.has(e.target).length === 0) {
                $sidebar.removeClass('show');
            }
        }
    });

    // Handle navigation items to auto-close on mobile
    $('.sidebar .nav-item').on('click', function() {
        if ($(window).width() <= 992) {
            $('.sidebar').removeClass('show');
        }
    });
});
