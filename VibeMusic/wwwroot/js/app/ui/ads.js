/**
 * ads.js - Ad sequence handling
 */

window.playAdSequence = async function() {
    if (window.adSequenceActive) return;
    window.adSequenceActive = true;
    const overlay = document.getElementById('adOverlay');
    const counter = document.getElementById('adCountdown');
    
    if (overlay && counter) {
        overlay.classList.remove('d-none');
        overlay.classList.add('d-flex');
        overlay.style.display = 'flex';
        
        let timeLeft = 5;
        counter.textContent = timeLeft;
        
        return new Promise(resolve => {
            const timer = setInterval(() => {
                timeLeft--;
                counter.textContent = timeLeft;
                if (timeLeft <= 0) {
                    clearInterval(timer);
                    overlay.classList.add('d-none');
                    overlay.classList.remove('d-flex');
                    overlay.style.display = 'none';
                    window.adSequenceActive = false;
                    resolve();
                }
            }, 1000);
        });
    }
};
