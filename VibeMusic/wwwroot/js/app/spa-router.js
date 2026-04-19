/**
 * spa-router.js - Intelligent Single Page Navigation for YoutubeMusicPlayer
 * Handles seamless page transitions while maintaining background audio playback.
 */

window.SPARouter = (function($) {
    const CONFIG = {
        containerSelector: '#spa-container',
        linkSelector: 'a:not([target="_blank"]):not([href^="#"]):not([data-no-spa])',
        activeClass: 'active',
        progressBarId: 'spa-progress-bar'
    };

    let isNavigating = false;

    function init() {
        console.log("[SPA-Router] Initializing...");
        setupProgressBar();
        attachLinkHandlers();
        handlePopState();
        
        // Initial setup for the currently loaded page
        reinitializePageScripts();
    }

    function setupProgressBar() {
        if ($(`#${CONFIG.progressBarId}`).length) return;
        $('body').append(`<div id="${CONFIG.progressBarId}" style="position:fixed; top:0; left:0; height:3px; background:var(--accent-color, #6C5CE7); z-index:9999; width:0; transition:width 0.3s ease; box-shadow:0 0 10px var(--accent-color);"></div>`);
    }

    function updateProgressBar(percent) {
        $(`#${CONFIG.progressBarId}`).css('width', `${percent}%`);
        if (percent >= 100) {
            setTimeout(() => $(`#${CONFIG.progressBarId}`).css('width', '0'), 300);
        }
    }

    function attachLinkHandlers() {
        $(document)
            .off('click.spa', CONFIG.linkSelector)
            .on('click.spa', CONFIG.linkSelector, function(e) {
                const href = $(this).attr('href');
                if (!href) return;

                // Explicitly skip download actions and links with 'download' attribute
                if ($(this).attr('download') !== undefined || href.includes('/Download')) {
                    return; 
                }
                
                // Basic validation for internal links
                if (href.startsWith('http') && !href.includes(window.location.hostname)) return;
                if (href.startsWith('javascript:')) return;

                e.preventDefault();
                navigateTo(href);
            });
    }

    async function navigateTo(url, pushState = true) {
        if (isNavigating) return;

        // Prevent reloading the same page
        const currentPath = window.location.pathname;
        const currentSearch = window.location.search;
        const targetUrl = new URL(url, window.location.origin);
        const targetPath = targetUrl.pathname;
        const targetSearch = targetUrl.search;

        if (currentPath === targetPath && currentSearch === targetSearch && pushState) {
            console.log("[SPA-Router] Already on this page, skipping navigation.");
            // If we are on Home, ensure we exit search mode
            if (currentPath === '/' && typeof window.exitSearchMode === 'function') {
                window.exitSearchMode();
            }
            $(CONFIG.containerSelector).animate({ scrollTop: 0 }, 'smooth');
            return;
        }

        isNavigating = true;
        console.log("[SPA-Router] Navigating to:", url);
        updateProgressBar(30);

        try {
            const response = await fetch(url, {
                headers: { 'X-SPA-Request': 'true' }
            });

            if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

            const html = await response.text();
            updateProgressBar(80);

            // Update DOM
            const $newContent = $(html);
            $(CONFIG.containerSelector).html($newContent);
            
            // Update Title if available in the partial (might need a hidden input or data-title)
            const newTitle = $newContent.find('#page-title-data').val() || $newContent.filter('title').text();
            if (newTitle) document.title = newTitle;

            // Scroll to top
            $(CONFIG.containerSelector).scrollTop(0);

            if (pushState) {
                window.history.pushState({ url }, "", url);
            }

            // Sync sidebar active state
            updateSidebarActive(url);

            // RE-INIT SCRIPTS
            reinitializePageScripts();

            updateProgressBar(100);
        } catch (error) {
            console.error("[SPA-Router] Navigation failed:", error);
            updateProgressBar(100);
            window.location.href = url; // Fallback to full reload
        } finally {
            isNavigating = false;
        }
    }

    function handlePopState() {
        window.onpopstate = function(event) {
            if (event.state && event.state.url) {
                navigateTo(event.state.url, false);
            } else {
                window.location.reload(); // Root state fallback
            }
        };
    }

    function updateSidebarActive(url) {
        $('.sidebar .nav-item').removeClass('active');
        // Match base path to highlight correctly
        const path = new URL(url, window.location.origin).pathname;
        $(`.sidebar .nav-item[href="${path}"]`).addClass('active');
    }

    function reinitializePageScripts() {
        console.log("[SPA-Router] Re-initializing page components...");
        
        // 1. Auto-execute script tags found in the partial
        $(CONFIG.containerSelector).find('script').each(function(i) {
            try {
                const scriptText = $(this).text();
                if (scriptText) {
                    console.log(`[SPA-Router] Executing script block #${i+1}...`);
                    eval(scriptText);
                }
            } catch (e) {
                console.warn("[SPA-Router] Script execution failed:", e);
            }
        });

        // 2. Re-init Home Page Sections if exists
        // Wait a tiny bit to ensure scripts are processed
        setTimeout(() => {
            if (typeof window.loadAllSections === 'function') {
                console.log("[SPA-Router] Found loadAllSections, calling...");
                window.loadAllSections();
            } else {
                console.log("[SPA-Router] loadAllSections NOT FOUND in window.");
            }
            
            if (typeof window.initSearchUI === 'function') {
                window.initSearchUI();
            }

            if (typeof window.onPageLoad === 'function') {
                window.onPageLoad();
            }
        }, 10);
    }

    return {
        init,
        navigateTo
    };
})(jQuery);

// Auto-boot if not in a partial context
$(function() {
    window.SPARouter.init();
});
