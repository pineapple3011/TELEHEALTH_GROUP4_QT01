// Ẩn/hiện phần Cấp cứu và Phòng khám theo hình thức khám.
// Đặt trong file riêng (không inline) để tương thích Content-Security-Policy: script-src 'self'.
document.addEventListener('DOMContentLoaded', function () {
    var emergency = document.getElementById('emergencySection');
    var clinic = document.getElementById('clinicSection');
    var radios = document.querySelectorAll('input[name="HinhThuc"]');

    function sync() {
        var checked = document.querySelector('input[name="HinhThuc"]:checked');
        var v = checked ? checked.value : '';
        if (emergency) emergency.style.display = (v === 'Cấp cứu') ? 'block' : 'none';
        if (clinic) clinic.style.display = (v === 'Offline') ? 'block' : 'none';
    }

    radios.forEach(function (r) { r.addEventListener('change', sync); });
    sync();
});
