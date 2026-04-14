/**
 * metadata.js - External data resolution and lyrics fetching (jQuery Refactored)
 */

window.playAnyTrack = async function(title, artist, thumb) {
    console.log(`[Metadata] Resolving external track: ${title} - ${artist}`);
    if (typeof toastr !== 'undefined') toastr.info(`Đang tìm nguồn cho: ${title}...`);
    
    const query = encodeURIComponent(`${title} ${artist}`);
    $.getJSON(`/Home/GetStreamUrl?query=${query}&title=${encodeURIComponent(title)}&artist=${encodeURIComponent(artist)}`)
        .done(function(data) {
            const result = data.success ? data.data : null;
            if (result && result.streamUrl) {
                if (typeof playSingleTrack === 'function') {
                    window.playSingleTrack(result.videoId || "", title, artist, thumb);
                }
            } else {
                if (typeof toastr !== 'undefined') toastr.error(data.message || "Không tìm thấy nguồn phát.");
            }
        })
        .fail(function() {
            if (typeof toastr !== 'undefined') toastr.error("Lỗi kết nối máy chủ khi tìm kiếm.");
        });
};

window.fetchRichMetadata = function(videoId, retryCount = 0, lang = null) {
    if (!videoId) return;

    const $lc = $('#lyricsContent');
    const $bc = $('#bioContent');
    
    if (retryCount === 0) {
        $lc.attr('class', 'lyrics-text').html(`
            <div class="lyrics-skeleton" style="width: 70%"></div>
            <div class="lyrics-skeleton" style="width: 85%"></div>
            <div class="lyrics-skeleton" style="width: 60%"></div>
        `);
        $bc.html('<span class="opacity-50 text-dim small">Đang tải tiểu sử nghệ sĩ...</span>');
    }

    const url = lang ? `/Home/GetRichMetadata?videoId=${encodeURIComponent(videoId)}&lang=${encodeURIComponent(lang)}` : `/Home/GetRichMetadata?videoId=${encodeURIComponent(videoId)}`;
    
    $.getJSON(url)
        .done(function(json) {
            let data = json.success ? json.data : (json.Data || (json.status === "SUCCESS" ? json : json));

            if (!data || (data.status === "NOT_FOUND" && retryCount >= 1)) {
                throw new Error("Metadata not found");
            }

            if ($lc.length) {
                renderLanguageSwitcher(videoId, data.availableCaptions, lang);

                if (data.status === "SUCCESS" && data.lyrics) {
                    let timedLyrics = data.timedLyrics || data.TimedLyrics || [];
                    let lyricsType = data.lyricsType || data.LyricsType || (timedLyrics.length > 0 ? "TIMED" : "PLAIN");

                    timedLyrics.sort((a, b) => {
                        const timeA = a.startTime ?? a.StartTime ?? a.offset ?? 0;
                        const timeB = b.startTime ?? b.StartTime ?? b.offset ?? 0;
                        return timeA - timeB;
                    });

                    if (lyricsType === "TIMED" && timedLyrics.length > 0) {
                        // Filter out empty lines
                        const validLines = timedLyrics.filter(line => (line.text || line.Text || "").trim().length > 0);
                        
                        if (validLines.length === 0) {
                             renderPlainLyrics($lc, data.lyrics);
                             return;
                        }

                        const linesHtml = validLines.map((line, index) => {
                            const start = parseFloat(line.startTime ?? line.StartTime ?? line.offset ?? line.Offset ?? 0);
                            const dur = parseFloat(line.duration ?? line.Duration ?? 0);
                            const text = line.text ?? line.Text ?? "";
                            
                            return `
                                <div class="lyrics-line" 
                                     id="lyric-line-${index}"
                                     data-start="${start.toFixed(3)}" 
                                     data-duration="${dur.toFixed(3)}"
                                     onclick="window.audioPlayer.currentTime = ${start}; window.audioPlayer.play();">
                                     ${text}
                                </div>
                            `;
                        }).join('');
                        
                        $lc.html(linesHtml)
                           .addClass('is-synchronized')
                           .removeClass('is-plain-text')
                           .removeClass('is-plain-subtitle')
                           .removeClass('has-active-line');
                        
                        if (typeof updateLyricsSync === 'function' && window.audioPlayer) {
                            setTimeout(() => updateLyricsSync(window.audioPlayer.currentTime), 100);
                        }
                    } else {
                        renderPlainLyrics($lc, data.lyrics);
                    }
                } else if (data.status === "NOT_FOUND" && retryCount < 1) {
                    setTimeout(() => fetchRichMetadata(videoId, retryCount + 1, lang), 3000);
                } else {
                    $lc.removeClass('lyrics-text').html(`
                        <div class="lyrics-not-found animate-fade-in">
                            <i class="fa-solid fa-music mb-3 opacity-20 d-block fs-1"></i>
                            <div class="opacity-80">Hiện tại chưa có lời bài hát cho tác phẩm này.</div>
                        </div>
                    `);
                }
                $lc.parent().scrollTop(0);
            }
            if ($bc.length) $bc.text(data.bio || "Thông tin nghệ sĩ đang được cập nhật...");
        })
        .fail(function() {
            $lc.html('<div class="lyrics-not-found text-danger opacity-50 small">Lỗi khi tải metadata.</div>');
            $bc.text("Không tìm thấy tiểu sử nghệ sĩ.");
        });
};

function renderPlainLyrics($container, lyrics) {
    const noticeHtml = `
        <div class="lyrics-plain-notice animate-fade-in">
            <i class="fa-solid fa-circle-info me-2"></i>
            Đây là phụ đề bài hát (không hỗ trợ chạy theo nhạc)
        </div>
    `;
    
    $container.html(`
        <div class="is-plain-subtitle animate-fade-in">
            ${noticeHtml}
            <div class="plain-text-content">
                ${lyrics.replace(/\n/g, '<br>')}
            </div>
        </div>
    `)
    .removeClass('is-synchronized')
    .addClass('is-plain-text')
    .removeClass('has-active-line');
}

function renderLanguageSwitcher(videoId, captions, currentLang) {
    const $container = $('#lyricsHeader');
    if (!$container.length || !captions || captions.length <= 1) {
        $container.empty();
        return;
    }

    const options = captions.map(c => `
        <option value="${c.languageCode}" ${c.languageCode === (currentLang || (c.isAutoGenerated ? c.languageCode : '')) ? 'selected' : ''}>
            ${c.languageName} ${c.isAutoGenerated ? '(Tự động)' : ''}
        </option>
    `).join('');

    $container.html(`
        <div class="caption-switcher">
            <i class="fa-solid fa-closed-captioning opacity-50 me-1"></i>
            <select onchange="fetchRichMetadata('${videoId}', 0, this.value)" class="form-select form-select-sm bg-transparent border-0 text-white-50 py-0">
                ${options}
            </select>
        </div>
    `);
}
