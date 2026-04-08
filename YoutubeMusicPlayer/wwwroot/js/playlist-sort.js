/**
 * playlist-sort.js - Handles drag-and-drop song reordering using SortableJS
 */

document.addEventListener('DOMContentLoaded', () => {
    const el = document.getElementById('playlistSongsTableBody');
    if (!el) return;

    const playlistId = el.dataset.playlistId;
    if (!playlistId) return;

    Sortable.create(el, {
        animation: 150,
        handle: '.drag-handle', // Drag handle selector
        ghostClass: 'sortable-ghost',
        onEnd: async function () {
            const songIds = Array.from(el.querySelectorAll('.song-row')).map(row => row.dataset.songId);
            
            try {
                const response = await fetch(`/Playlist/Reorder?playlistId=${playlistId}`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(songIds)
                });

                if (response.ok) {
                    toastr.success("Đã cập nhật thứ tự bài hát.");
                    // Update index numbers in the UI
                    updateRowIndexes();
                } else {
                    toastr.error("Không thể lưu thứ tự bài hát.");
                }
            } catch (error) {
                console.error('Error reordering songs:', error);
                toastr.error("Lỗi kết nối máy chủ.");
            }
        }
    });

    function updateRowIndexes() {
        const indexes = el.querySelectorAll('.row-index');
        indexes.forEach((span, i) => {
            span.textContent = i + 1;
        });
    }
});
