/* Điều hướng AJAX: bấm link / lọc không load lại cả trang, chỉ thay #main-content */
(function ($) {
    var $main = $('#main-content');
    if (!$main.length) return;

    var loading = null;

    function isInternal(a) {
        return a.protocol === location.protocol && a.host === location.host;
    }

    function shouldHandle(e, a) {
        if (e.isDefaultPrevented()) return false;
        if (e.which !== 1 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return false;
        if (a.target && a.target !== '_self') return false;
        if (a.hasAttribute('download') || a.hasAttribute('data-no-ajax')) return false;
        var href = a.getAttribute('href');
        if (!href || href.charAt(0) === '#' || /^(mailto:|tel:|javascript:)/i.test(href)) return false;
        if (!isInternal(a)) return false;
        // cùng trang, chỉ khác hash -> để trình duyệt tự xử lý
        if (a.pathname === location.pathname && a.search === location.search && a.hash) return false;
        return true;
    }

    function setActiveMenu(path) {
        var best = null, bestLen = -1;
        $('.classynav ul li').removeClass('active').each(function () {
            var a = $(this).children('a')[0];
            if (!a) return;
            var p = a.pathname.replace(/\/$/, '') || '/';
            var cur = path.replace(/\/$/, '') || '/';
            if ((cur === p || (p !== '/' && cur.indexOf(p + '/') === 0)) && p.length > bestLen) {
                best = this; bestLen = p.length;
            }
        });
        if (best) $(best).addClass('active');
    }

    // keepScroll = true: giữ nguyên vị trí cuộn (dùng khi lọc danh sách)
    function load(url, push, keepScroll) {
        if (loading) loading.abort();
        $main.css('opacity', 0.4);

        loading = $.ajax({ url: url, method: 'GET', dataType: 'html', cache: false })
            .done(function (html, status, xhr) {
                // Server lỡ trả cả trang đầy đủ (không phải bản rút gọn) -> load trang bình thường
                if (/<html[\s>]/i.test(html) || html.indexOf('id="main-content"') !== -1) {
                    location.href = url;
                    return;
                }

                // Nếu server redirect thì lấy URL cuối cùng
                var finalUrl = xhr.responseURL ? xhr.responseURL.replace(/([?&])_=\d+&?/, '$1').replace(/[?&]$/, '') : url;

                // .html() của jQuery sẽ chạy luôn các <script> inline trong view
                $main.html(html);

                var title = $main.find('#ajax-meta').attr('data-title');
                if (title) document.title = title;

                if (push) history.pushState({ ajax: true }, '', finalUrl);
                setActiveMenu(new URL(finalUrl, location.href).pathname);

                // Khởi tạo lại validation cho form mới (nếu có)
                if ($.validator && $.validator.unobtrusive) $.validator.unobtrusive.parse($main);
                if ($.fn.tooltip) $('[data-toggle="tooltip"]').tooltip();

                // Chuyển trang -> về đầu trang (vẫn thấy hero). Lọc danh sách -> giữ nguyên vị trí.
                if (!keepScroll) $('html, body').stop().animate({ scrollTop: 0 }, 250);
            })
            .fail(function (xhr, status) {
                if (status === 'abort') return;
                location.href = url;          // lỗi -> fallback load trang bình thường
            })
            .always(function () {
                $main.css('opacity', 1);
                loading = null;
            });
    }

    // Bắt click trên toàn bộ link nội bộ (menu, nút trong trang, ...)
    $(document).on('click', 'a', function (e) {
        if (!shouldHandle(e, this)) return;
        e.preventDefault();
        load(this.href, true);
    });

    // Form lọc (GET): <form data-ajax-get> -> gửi bằng AJAX
    $(document).on('submit', 'form[data-ajax-get]', function (e) {
        e.preventDefault();
        var base = this.action.split('?')[0];
        var qs = $(this).serialize();
        load(base + (qs ? '?' + qs : ''), true, true);
    });

    // Ô nào có data-autosubmit thì đổi giá trị là tự gửi form
    $(document).on('change', 'form[data-ajax-get] [data-autosubmit]', function () {
        $(this.form).trigger('submit');
    });

    // Nút Back/Forward
    window.addEventListener('popstate', function () {
        load(location.href, false);
    });

    setActiveMenu(location.pathname);
})(jQuery);