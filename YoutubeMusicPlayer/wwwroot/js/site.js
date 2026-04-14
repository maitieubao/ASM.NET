function handlePremiumDownload(url) {
    // Priority 1: Check from Global Config (most reliable)
    let isPremium = false;
    if (window.YTM_CONFIG && window.YTM_CONFIG.isPremium !== undefined) {
        isPremium = window.YTM_CONFIG.isPremium === true || window.YTM_CONFIG.isPremium === "true";
    } else {
        isPremium = window.userIsPremium === true || window.userIsPremium === "true";
    }
    
    if (isPremium) {
        // Force download
        const link = document.createElement('a');
        link.href = url;
        link.setAttribute('download', '');
        link.setAttribute('data-no-spa', 'true');
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } else {
        // Use a more accessible dialog
        alert("Tính năng tải nhạc chỉ dành cho thành viên Premium. Vui lòng nâng cấp gói hội viên của bạn để sử dụng chức năng này!");
        window.location.href = "/Subscription";
    }
}
