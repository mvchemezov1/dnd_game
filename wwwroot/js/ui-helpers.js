// wwwroot/js/ui-helpers.js
// Вспомогательные функции для UI: уведомления, загрузка, тёмная тема

// ---------- Toast-уведомления ----------
function showToast(message, type = 'info', title = null) {
    const container = document.querySelector('.toast-container') || createToastContainer();
    const toastId = 'toast-' + Date.now();

    const colors = {
        success: 'bg-success text-white',
        error: 'bg-danger text-white',
        warning: 'bg-warning text-dark',
        info: 'bg-info text-dark'
    };
    const bgClass = colors[type] || colors.info;
    const iconMap = {
        success: '✅',
        error: '❌',
        warning: '⚠️',
        info: 'ℹ️'
    };

    const toastEl = document.createElement('div');
    toastEl.className = 'toast align-items-center border-0 show';
    toastEl.id = toastId;
    toastEl.role = 'alert';
    toastEl.ariaLive = 'assertive';
    toastEl.ariaAtomic = 'true';
    toastEl.innerHTML = `
        <div class="d-flex ${bgClass} rounded-3">
            <div class="toast-body d-flex align-items-center gap-2">
                <span style="font-size:1.2rem;">${iconMap[type] || 'ℹ️'}</span>
                    <div>
                        ${title ? `<strong>${title}</strong><br>` : ''}
                        ${message}
                    </div>
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
    `;
    container.appendChild(toastEl);

    // Автоматическое скрытие через 5 секунд
    setTimeout(() => {
        const toast = document.getElementById(toastId);
        if (toast) {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }
    }, 5000);
}

function createToastContainer() {
    const container = document.createElement('div');
    container.className = 'toast-container';
    document.body.appendChild(container);
    return container;
}

// ---------- Индикатор загрузки ----------
let loadingOverlay = null;

function showLoading(message = 'Загрузка...') {
    if (loadingOverlay) return;
    const overlay = document.createElement('div');
    overlay.className = 'loading-overlay';
    overlay.id = 'loadingOverlay';
    overlay.innerHTML = `
        <div class="text-center text-white">
            <div class="spinner"></div>
            <div class="mt-3">${message}</div>
        </div>
    `;
    document.body.appendChild(overlay);
    loadingOverlay = overlay;
}

function hideLoading() {
    if (loadingOverlay) {
        loadingOverlay.remove();
        loadingOverlay = null;
    }
}

// ---------- Тёмная тема ----------
function getPreferredTheme() {
    // Сначала проверяем localStorage
    const saved = localStorage.getItem('theme');
    if (saved) return saved;

    // Если нет сохранённого, проверяем настройки браузера
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
        return 'dark';
    }

    // По умолчанию – светлая
    return 'light';
}

function applyTheme(theme) {
    const html = document.documentElement;
    if (theme === 'dark') {
        html.setAttribute('data-bs-theme', 'dark');
        html.style.colorScheme = 'dark';
    } else {
        html.removeAttribute('data-bs-theme');
        html.style.colorScheme = 'light';
    }
    localStorage.setItem('theme', theme);
    // Обновляем состояние переключателя, если он есть
    const toggle = document.getElementById('themeToggle');
    if (toggle) {
        toggle.checked = (theme === 'dark');
    }
}

function toggleTheme() {
    const current = document.documentElement.getAttribute('data-bs-theme');
    const newTheme = (current === 'dark') ? 'light' : 'dark';
    applyTheme(newTheme);
    showToast(`Тема: ${newTheme === 'dark' ? '🌙 Тёмная' : '☀️ Светлая'}`, 'info');
}

// Инициализация темы при загрузке
document.addEventListener('DOMContentLoaded', () => {
    const theme = getPreferredTheme();
    applyTheme(theme);

    // Создаём переключатель, если его нет, но только если есть элемент #themeTogglePlaceholder
    const placeholder = document.getElementById('themeTogglePlaceholder');
    if (placeholder) {
        const toggleHtml = `
            <div class="form-check form-switch d-inline-block ms-2 me-2">
                <input class="form-check-input" type="checkbox" id="themeToggle" ${theme === 'dark' ? 'checked' : ''}>
                <label class="form-check-label" for="themeToggle">
                    <span id="themeIcon">${theme === 'dark' ? '🌙' : '☀️'}</span>
                </label>
            </div>
        `;
        placeholder.innerHTML = toggleHtml;
        document.getElementById('themeToggle').addEventListener('change', (e) => {
            const newTheme = e.target.checked ? 'dark' : 'light';
            applyTheme(newTheme);
            document.getElementById('themeIcon').textContent = newTheme === 'dark' ? '🌙' : '☀️';
        });
    }
});

// ---------- Обёртка для fetch с автоматическим показом загрузки ----------
async function apiFetch(url, options = {}, showLoader = true) {
    if (showLoader) showLoading();
    try {
        // Используем authFetch, если он определён
        const fetchFn = typeof authFetch === 'function' ? authFetch : fetch;
        const response = await fetchFn(url, options);
        return response;
    } finally {
        if (showLoader) hideLoading();
    }
}

// Глобальный перехватчик для показа ошибок
window.addEventListener('unhandledrejection', (event) => {
    showToast('Необработанная ошибка: ' + (event.reason?.message || 'Unknown error'), 'error');
});

// Экспортируем в глобальную область
window.showToast = showToast;
window.showLoading = showLoading;
window.hideLoading = hideLoading;
window.applyTheme = applyTheme;
window.toggleTheme = toggleTheme;
window.apiFetch = apiFetch;