// localStorage can throw (private windows, blocked site data), so every access is guarded.
window.elForecast = {
    storageGet: function (key) {
        try { return window.localStorage.getItem(key); } catch { return null; }
    },
    storageSet: function (key, value) {
        try { window.localStorage.setItem(key, value); } catch { /* ignore */ }
    }
};

// Update check. A web app pinned to the iPad home screen keeps serving its cached index.html
// (GitHub Pages allows 10 minutes, iOS often keeps it longer, and a resumed app does not reload
// at all), so a new deploy may never show up. The import map in index.html lists the
// fingerprinted framework files, and dotnet.js's fingerprint changes with every build of the app,
// so comparing the running page's import map with a freshly fetched one tells whether a new
// version is out. If so, the HTTP cache is refreshed for index.html and app.css and the page
// reloads. Runs at start-up and whenever the app comes back to the foreground.
(function () {
    var importMapPattern = /<script type="importmap">([\s\S]*?)<\/script>/;
    var reloadKey = 'elForecast.reloadedFor';
    var checking = false;

    // Whitespace is dropped before comparing: the HTML parser turns the file's CRLF into LF.
    function normalize(text) {
        return text.replace(/\s+/g, '');
    }

    function currentMap() {
        var el = document.querySelector('script[type="importmap"]');
        return el ? normalize(el.textContent) : null;
    }

    function sessionGet(key) {
        try { return window.sessionStorage.getItem(key); } catch { return null; }
    }

    function sessionSet(key, value) {
        try { window.sessionStorage.setItem(key, value); } catch { /* ignore */ }
    }

    async function check() {
        var running = currentMap();
        if (checking || !running || !navigator.onLine)
            return;

        checking = true;
        try {
            var response = await fetch('./', { cache: 'no-store' });
            if (!response.ok)
                return;

            var match = importMapPattern.exec(await response.text());
            var latest = match ? normalize(match[1]) : null;
            if (!latest || latest === running)
                return;

            // Only one reload per new version, so a stubborn cache can't cause a reload loop.
            if (sessionGet(reloadKey) === latest)
                return;
            sessionSet(reloadKey, latest);

            // "reload" fetches from the server and stores the result in the HTTP cache, so the
            // page load that follows picks up the new files.
            await Promise.all([
                fetch('./', { cache: 'reload' }),
                fetch('css/app.css', { cache: 'reload' })
            ]);
            window.location.reload();
        } catch {
            // Offline or blocked: keep running the version we have.
        } finally {
            checking = false;
        }
    }

    window.addEventListener('load', check);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible')
            check();
    });
})();
