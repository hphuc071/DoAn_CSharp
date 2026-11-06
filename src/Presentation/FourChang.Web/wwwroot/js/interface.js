/* Hành vi UI dùng chung. Không gọi API, không tính giá, không ghi hồ sơ/giỏ hàng vào storage. */
(() => {
    const motion = window.matchMedia('(prefers-reduced-motion: reduce)');

    // Tạo nút hiện/ẩn bên ngoài label: giữ tên truy cập của input và autocomplete/password manager.
    document.querySelectorAll('input[type="password"]').forEach(input => {
        const label = input.closest('label.field');
        if (!label) return;
        const wrapper = document.createElement('div');
        wrapper.className = 'password-control';
        label.before(wrapper); wrapper.append(label);
        const toggle = document.createElement('button');
        toggle.type = 'button'; toggle.className = 'password-toggle';
        toggle.setAttribute('aria-label', 'Hiện mật khẩu'); toggle.setAttribute('aria-pressed', 'false');
        toggle.innerHTML = '<svg class="ui-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/></svg>';
        toggle.addEventListener('click', () => {
            const visible = input.type === 'password';
            input.type = visible ? 'text' : 'password';
            toggle.setAttribute('aria-label', visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
            toggle.setAttribute('aria-pressed', String(visible));
        });
        wrapper.append(toggle);
    });

    // Đồng bộ trạng thái trợ năng với class selected do site.js hiện có quản lý.
    function choices(group) {
        group.querySelectorAll('button').forEach(button => button.setAttribute('aria-pressed', String(button.classList.contains('selected'))));
    }
    document.querySelectorAll('[data-choice],#categories').forEach(group => {
        group.setAttribute('role', 'group'); choices(group);
        group.addEventListener('click', () => queueMicrotask(() => choices(group)));
    });

    // Giữ nguyên sự kiện chọn hình thức nhận hàng; chỉ thêm điều hướng bàn phím/aria.
    document.querySelectorAll('[data-tabs]').forEach(tabs => {
        const buttons = [...tabs.querySelectorAll('[data-tab]')];
        tabs.setAttribute('role', 'tablist');
        function sync() {
            buttons.forEach(button => {
                const selected = button.classList.contains('selected');
                button.setAttribute('role', 'tab'); button.setAttribute('aria-selected', String(selected));
                button.setAttribute('aria-controls', button.dataset.tab); button.tabIndex = selected ? 0 : -1;
                const panel = document.getElementById(button.dataset.tab);
                panel?.setAttribute('role', 'tabpanel');
            });
        }
        sync(); tabs.addEventListener('click', () => queueMicrotask(sync));
        tabs.addEventListener('keydown', event => {
            const index = buttons.indexOf(document.activeElement);
            if (index < 0) return;
            const next = event.key === 'ArrowRight' ? (index + 1) % buttons.length
                : event.key === 'ArrowLeft' ? (index - 1 + buttons.length) % buttons.length
                : event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : null;
            if (next === null) return;
            event.preventDefault(); buttons[next].click(); buttons[next].focus();
        });
    });

    // Kết quả tìm kiếm là số card thực đang hiển thị; không phải số liệu bán hàng hoặc KPI.
    const grid = document.getElementById('product-grid');
    const count = document.getElementById('menu-result-count');
    if (grid && count) {
        let last = '';
        const update = () => {
            const text = `${grid.querySelectorAll('.product-card:not([hidden])').length} món phù hợp`;
            if (text !== last) { count.textContent = text; last = text; }
        };
        new MutationObserver(update).observe(grid, { attributes: true, attributeFilter: ['hidden'], subtree: true, childList: true });
        update();
    }

    // Trên điện thoại đưa khách tới phần tùy chỉnh sẵn có, không tạo giỏ hay đơn ở bước này.
    document.querySelectorAll('[data-product]').forEach(button => button.addEventListener('click', () => {
        if (!window.matchMedia('(max-width: 900px)').matches) return;
        document.querySelector('.customizer')?.scrollIntoView({ block: 'start', behavior: motion.matches ? 'instant' : 'smooth' });
    }));

    // Gắn lỗi server vào đúng field nếu có; lỗi chung được focus một lần sau submit thất bại.
    document.querySelectorAll('[data-valmsg-for]').forEach(message => {
        const input = document.getElementsByName(message.dataset.valmsgFor)[0];
        if (!input || !message.textContent.trim()) return;
        message.id ||= `${input.id}-error`; input.setAttribute('aria-describedby', message.id); input.setAttribute('aria-invalid', 'true');
    });
    const error = document.querySelector('.validation-summary-errors, .account-error[role="alert"]');
    if (error?.textContent.trim()) { error.tabIndex = -1; error.focus({ preventScroll: false }); }

    // Chỉ phản hồi trạng thái gửi form thật; không ngăn submit hoặc thay antiforgery/idempotency token.
    document.querySelectorAll('form[method="post"]:not([data-demo-form])').forEach(form => {
        form.addEventListener('submit', event => queueMicrotask(() => {
            if (event.defaultPrevented) return;
            form.setAttribute('aria-busy', 'true');
            form.querySelectorAll('button[type="submit"],button:not([type])').forEach(button => {
                button.dataset.originalText = button.textContent; button.textContent = 'Đang xử lý…';
            });
        }));
    });
    window.addEventListener('pageshow', () => {
        document.querySelectorAll('form[aria-busy]').forEach(form => form.removeAttribute('aria-busy'));
        document.querySelectorAll('[data-original-text]').forEach(button => { button.textContent = button.dataset.originalText; delete button.dataset.originalText; });
    });
})();
