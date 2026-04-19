'use strict';
function isPlaylistOrWeeklyCompilation(title, author) {
    const t = title.toLowerCase();
    const a = (author || '').toLowerCase();
    if (/\bof\s+the\s+(week|month|year)\b/.test(t)) return true;
    if (/\bthis\s+(week|month)\b/.test(t) && (t.includes('song') || t.includes('music') || t.includes('hit') || t.includes('new'))) return true;
    if (/\bnew\s+music\s+(friday|monday|tuesday|wednesday|thursday|saturday|sunday)\b/.test(t)) return true;
    if (/\b(weekly|monthly)\s+(hits|songs|music|playlist|mix|chart)\b/.test(t)) return true;
    if (/^new\s+songs?\s*\(/.test(t) || /^new\s+music\s*\(/.test(t)) return true;
    if (/^new\s+(songs|releases|music|hits)\b/.test(t) && (t.includes('week') || t.includes('month') || /\b(january|february|march|april|may|june|july|august|september|october|november|december)\b/.test(t) || /\b20\d\d\b/.test(t))) return true;
    if (/\bbest\s+(new\s+)?(songs|music|hits)\b/.test(t) && (t.includes('week') || t.includes('month') || /\b20\d\d\b/.test(t))) return true;
    if (/\b(hot|trending|popular)\s+this\s+(week|month)\b/.test(t)) return true;
    const compilationChannels = ['inmusic', 'in music', 'new music', 'music weekly', 'weekly music', 'songs weekly', 'music friday', 'new releases', 'fresh music', 'music chart', 'chart music', 'hit music', 'music playlist', 'playlist music'];
    if (compilationChannels.some(k => a.includes(k))) return true;
    if (/\b(songs|hits|music)\s+of\s+(january|february|march|april|may|june|july|august|september|october|november|december)\b/.test(t)) return true;
    if (/\b(january|february|march|april|may|june|july|august|september|october|november|december|spring|summer|fall|autumn|winter)\s+mix\b/.test(t)) return true;
    return false;
}

const tests = [
    ['New Songs Of The Week (April 3, 2026)', 'InMusic Official', true],
    ['New Songs Of The Week (February 2026)', 'InMusic Official', true],
    ['New Songs Of The Week (March 27,...)', 'InMusic Official', true],
    ['New Music Friday April 2026', 'Spotify', true],
    ['Best New Songs This Week', 'Music Channel', true],
    ['Weekly Hits 2026', 'Top Music', true],
    ['Songs Of April 2026', 'Various', true],
    ['New Songs (April 3, 2026)', 'InMusic', true],
    ['Hot This Week Music', 'Channel', true],
    ['Trending This Month', 'Music', true],
    ['New Releases April 2026', 'Music', true],
    ['Best Songs This Month 2026', 'Channel', true],
    ['Flowers - Miley Cyrus (Official MV)', 'Miley Cyrus', false],
    ['Tôi Thấy Hoa Vàng Trên Cỏ Xanh - Official Audio', 'Bùi Anh Tuấn', false],
    ['Top of the World - Carpenters', 'Carpenters', false],
    ['New Song - Official MV 2026', 'Artist Name', false],
    ['Blinding Lights (Official Video)', 'The Weeknd', false],
    ['Hoa Nở Không Màu - Hoài Lâm', 'Hoài Lâm', false],
];

let passed = 0; let failed = 0;
tests.forEach(([title, author, expected]) => {
    const result = isPlaylistOrWeeklyCompilation(title, author);
    const ok = result === expected;
    if (ok) { console.log('  PASS: ' + title.substring(0, 55)); passed++; }
    else { console.error('  FAIL: ' + title.substring(0, 55) + ' -> got ' + result + ', expected ' + expected); failed++; }
});
console.log('\nResults: ' + passed + ' passed, ' + failed + ' failed');
process.exit(failed > 0 ? 1 : 0);
