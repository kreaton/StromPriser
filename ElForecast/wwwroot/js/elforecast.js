// localStorage can throw (private windows, blocked site data), so every access is guarded.
window.elForecast = {
    storageGet: function (key) {
        try { return window.localStorage.getItem(key); } catch { return null; }
    },
    storageSet: function (key, value) {
        try { window.localStorage.setItem(key, value); } catch { /* ignore */ }
    }
};
