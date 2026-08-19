// wwwroot/js/auth.js
// Общий модуль ролевого доступа для фронтенда.
// Поддерживает автоматическое обновление JWT через refresh-токен.

// ClaimTypes.Role сериализуется в JWT под полным URI, а не просто "role"
const ROLE_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role';
const NAME_CLAIM = 'unique_name';

// ---------- Базовые функции работы с токенами ----------

function getToken() {
    return localStorage.getItem('token');
}

function getRefreshToken() {
    return localStorage.getItem('refreshToken');
}

function setTokens(accessToken, refreshToken) {
    if (accessToken) localStorage.setItem('token', accessToken);
    if (refreshToken) localStorage.setItem('refreshToken', refreshToken);
}

function clearTokens() {
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
}

function decodeToken(token) {
    try {
        const payload = token.split('.')[1];
        const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
        const json = decodeURIComponent(
            atob(base64).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join('')
        );
        return JSON.parse(json);
    } catch (e) {
        return null;
    }
}

function getPayload() {
    const token = getToken();
    return token ? decodeToken(token) : null;
}

function getRole() {
    const payload = getPayload();
    return payload ? (payload[ROLE_CLAIM] || payload.role || null) : null;
}

function getUsername() {
    const payload = getPayload();
    return payload ? (payload[NAME_CLAIM] || null) : null;
}

function isTokenExpired() {
    const payload = getPayload();
    if (!payload || !payload.exp) return true;
    return Date.now() >= payload.exp * 1000;
}

function isLoggedIn() {
    return !!getToken() && !isTokenExpired();
}

// ---------- Навигация и роли ----------

function homeForRole(role) {
    if (role === 'GameMaster') return '/dm/combat.html';
    if (role === 'Admin') return '/dev/dashboard.html';
    return '/player/characters.html';
}

function logout() {
    clearTokens();
    window.location.href = '/login.html';
}

// Вызывать в начале защищённой страницы. allowedRoles — массив, например ['GameMaster','Admin'].
function requireRole(allowedRoles) {
    if (!isLoggedIn()) {
        window.location.href = '/login.html';
        throw new Error('redirecting to login');
    }
    const role = getRole();
    if (!allowedRoles.includes(role)) {
        document.body.innerHTML = `
            <div style="max-width:500px;margin:60px auto;text-align:center;font-family:sans-serif;">
                <h1>⛔ Доступ запрещён</h1>
                <p>Эта страница доступна роли: <b>${allowedRoles.join(' или ')}</b>.<br>Ваша роль: <b>${role ?? 'неизвестна'}</b>.</p>
                <a href="${homeForRole(role)}">← На свою главную</a>
            </div>`;
        throw new Error('access denied');
    }
}

// ---------- Авторизация (логин, регистрация, refresh) ----------

async function refreshToken() {
    const refresh = getRefreshToken();
    if (!refresh) {
        console.warn('No refresh token available');
        return null;
    }
    try {
        const res = await fetch('/api/auth/refresh', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken: refresh })
        });
        if (!res.ok) {
            // Refresh не удался – очищаем токены и разлогиниваем
            clearTokens();
            return null;
        }
        const data = await res.json();
        if (!data.token) {
            clearTokens();
            return null;
        }
        // Сохраняем новые токены (сервер может вернуть и refresh, если он ротируется)
        setTokens(data.token, data.refreshToken || refresh);
        return data.token;
    } catch (e) {
        console.error('Refresh error:', e);
        clearTokens();
        return null;
    }
}

// ---------- Перехватчик fetch с автоматическим обновлением токена ----------

// Хранилище очереди запросов, ожидающих refresh
let refreshPromise = null;
const pendingQueue = [];

async function authFetch(url, options = {}) {
    // Клонируем options, чтобы не мутировать исходный
    const opts = { ...options };

    // Добавляем заголовок авторизации, если он не задан явно
    if (!opts.headers) opts.headers = {};
    if (!opts.headers['Authorization']) {
        const token = getToken();
        if (token) {
            opts.headers['Authorization'] = `Bearer ${token}`;
        }
    }

    // Делаем запрос
    let response = await fetch(url, opts);

    // Если 401 и есть refresh-токен – пробуем обновить
    if (response.status === 401 && getRefreshToken()) {
        // Если уже есть процесс обновления – ждём его
        if (!refreshPromise) {
            refreshPromise = refreshToken()
                .then(newToken => {
                    // После успешного обновления токена, выполняем отложенные запросы
                    pendingQueue.forEach(({ resolve, opts }) => {
                        // Обновляем заголовок авторизации и повторяем
                        if (opts.headers) {
                            opts.headers['Authorization'] = `Bearer ${newToken}`;
                        }
                        resolve(fetch(opts.url, opts));
                    });
                    pendingQueue.length = 0;
                    return newToken;
                })
                .catch(err => {
                    // Если refresh не удался – отклоняем все отложенные запросы
                    pendingQueue.forEach(({ reject }) => reject(err));
                    pendingQueue.length = 0;
                    throw err;
                })
                .finally(() => {
                    refreshPromise = null;
                });
        }

        // Если refresh уже запущен – помещаем в очередь
        return new Promise((resolve, reject) => {
            pendingQueue.push({ resolve, reject, opts });
        });
    }

    return response;
}

// ---------- Обёртка для authHeaders (теперь не нужна, но оставляем для совместимости) ----------

function authHeaders(extra = {}) {
    const token = getToken();
    return {
        'Authorization': token ? `Bearer ${token}` : '',
        ...extra
    };
}

// ---------- Рисование навигации ----------

function renderNav(containerId = 'nav') {
    const el = document.getElementById(containerId);
    if (!el) return;

    const role = getRole();
    let html = `<a href="/">Главная</a>`;

    if (isLoggedIn()) {
        html += `<a href="/player/characters.html">Персонажи</a>`;
        html += `<a href="/player/game.html">Игра</a>`;
        html += `<a href="/player/crafting.html">Крафт</a>`;
        html += `<a href="/player/trade.html">Торговля</a>`;
        html += `<a href="/player/dialog.html">Диалог</a>`;
        html += `<a href="/player/travel.html">Путешествия</a>`;

        if (role === 'GameMaster' || role === 'Admin') {
            html += `<a href="/dm/combat.html">⚔️ Бой (ДМ)</a>`;
            html += `<a href="/dm/campaign.html">📜 Кампания (ДМ)</a>`;
        } else {
            html += `<a href="/player/combat.html">⚔️ Бой</a>`;
            html += `<a href="/player/campaign.html">📜 Кампания</a>`;
        }

        if (role === 'Admin') {
            html += `<a href="/dev/dashboard.html">🛠️ Разработчик</a>`;
        }

        const name = getUsername();
        html += `<a href="#" onclick="logout();return false;">Выйти${name ? ' (' + name + ')' : ''}</a>`;
    } else {
        html += `<a href="/login.html">Вход</a>`;
        html += `<a href="/register.html">Регистрация</a>`;
    }

    el.innerHTML = html;
}

// ---------- Экспортируем в глобальную область для использования в HTML ----------
window.getToken = getToken;
window.getRefreshToken = getRefreshToken;
window.setTokens = setTokens;
window.clearTokens = clearTokens;
window.getPayload = getPayload;
window.getRole = getRole;
window.getUsername = getUsername;
window.isLoggedIn = isLoggedIn;
window.logout = logout;
window.requireRole = requireRole;
window.authHeaders = authHeaders;
window.renderNav = renderNav;
window.authFetch = authFetch;
window.refreshToken = refreshToken;
// Инициализация темы при загрузке auth.js
document.addEventListener('DOMContentLoaded', () => {
    // applyTheme вызывается из ui-helpers.js, но мы вызываем её явно после загрузки
    if (typeof applyTheme === 'function') {
        const theme = localStorage.getItem('theme') ||
            (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
        applyTheme(theme);
    }
});

// Переопределяем logout, чтобы показывать уведомление
const originalLogout = logout;
logout = function () {
    clearTokens();
    if (typeof showToast === 'function') {
        showToast('Вы вышли из системы', 'warning');
    }
    setTimeout(() => {
        window.location.href = '/login.html';
    }, 500);
};