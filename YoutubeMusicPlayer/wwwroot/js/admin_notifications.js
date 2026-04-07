/**
 * Admin Notifications Utility
 * Automatically displays TempData["Success"] and TempData["Error"] as Toastr notifications.
 */
$(document).ready(function () {
    // Configure Toastr
    toastr.options = {
        "closeButton": true,
        "debug": false,
        "newestOnTop": true,
        "progressBar": true,
        "positionClass": "toast-top-right",
        "preventDuplicates": false,
        "onclick": null,
        "showDuration": "300",
        "hideDuration": "1000",
        "timeOut": "5000",
        "extendedTimeOut": "1000",
        "showEasing": "swing",
        "hideEasing": "linear",
        "showMethod": "fadeIn",
        "hideMethod": "fadeOut"
    };

    // Helper to get message from hidden inputs or logic
    const successMsg = $('#tempDataSuccess').val();
    const errorMsg = $('#tempDataError').val();

    if (successMsg) {
        toastr.success(successMsg, "Thành công");
    }

    if (errorMsg) {
        toastr.error(errorMsg, "Lỗi hệ thống");
    }
});
