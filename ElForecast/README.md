# ElForecast

A Blazor WebAssembly app (.NET 10) that shows Danish household electricity prices hour by
hour for about a week ahead. It uses published prices where they exist and the
stromligning.dk spot price forecast for the rest. The UI is in Danish; how to use it is
described in the [main README](../README.md#elforecast-the-week-ahead-in-the-browser).

It is built on the [`Stromligning`](../Stromligning/README.md) library, the same library the
StromPriser WinForms app uses. The library runs unchanged in the browser: the API sends
`Access-Control-Allow-Origin: *`, so the app calls it directly with no backend.

## Running

```bash
dotnet run --project ElForecast/ElForecast.csproj
```

Then open <http://localhost:5276>.

## Deployment

[`.github/workflows/deploy-elforecast.yml`](../.github/workflows/deploy-elforecast.yml)
publishes the app and uploads `wwwroot/` by FTP. It runs on every push to `main` that touches
`ElForecast/`, `Stromligning/` or the workflow itself, and can also be started by hand from the
Actions tab. After the first run only changed files are uploaded. It uses these repository
secrets:

| Secret         | Example |
|----------------|---------|
| `FTP_HOST`     | `ftp.example.com` |
| `FTP_USERNAME` | |
| `FTP_PASSWORD` | |
| `FTP_FOLDER`   | `webroots/www/elforecast` (no trailing `/`) |

The output is plain static files, so it can be hosted anywhere:

- `index.html` uses a relative `<base href="./">`, so the app works in any folder.
- The app has a single route. The setup view is `?vis=opsaetning` on the same page rather
  than its own path, so reloading or bookmarking it works without a server-side fallback to
  `index.html`.

## Version

The footer shows `Major.Minor.Build`. `MajorMinor` is set by hand in `ElForecast.csproj`. The
build number is the GitHub Actions run number, passed as `-p:BuildNumber=…`, and is `0` for
local builds. The copyright line comes from `<Copyright>` in the same file.

## The chart

- One stacked bar per hour from the current hour. Each bar is split into spot price, surcharge,
  net tariff, system tariff, distribution and electricity tax. The spot price is at the bottom
  because it is the part that changes.
- Horizontal gridlines every 0.50 kr. The price axis stays fixed while the bars scroll
  sideways when they do not fit the width.
- A vertical line marks each midnight, and the date is written under it. A dashed line labelled
  *Prognose →* marks where published prices end. Forecast bars are drawn lighter.
- The cheapest and most expensive hours are labelled with their total price.
- Hovering over a bar (or tapping it) shows its components, the total ex. VAT, the VAT and the
  total incl. VAT. Scrolling the chart hides the tooltip.
- *Vis som tabel* shows the same data as a table.
- A negative spot price is drawn below the zero line.
- The series colors are a colorblind-checked categorical palette, with separate steps for dark
  mode (see the `--s1`…`--s6` tokens in `app.css`).

The forecast rows are estimated as described in the
[library README](../Stromligning/README.md#how-forecast-rows-are-estimated).

## Saved settings

The selections are stored as JSON under the key `elforecast.setup` in the browser's
`localStorage`. Without a saved grid company, the front page links to the setup. A saved
company or product that no longer exists is replaced by the default. If storage is blocked,
the settings only last until the page is closed. Prices are fetched again when you come back
from the setup with a different grid company, customer group or product.

## Source files

| File | Responsibility |
|---|---|
| `Program.cs` | Sets the da-DK culture and registers the library's client and `PriceService`. |
| `AppInfo.cs` | Version and copyright from the assembly, shown in the footer. |
| `Services/ForecastSetup.cs` | The saved selections. |
| `Services/SetupStore.cs` | Reads and writes them in `localStorage` through `wwwroot/js/elforecast.js`. |
| `Services/CatalogService.cs` | Fetches the grid and trading companies once per visit, sorted the Danish way. |
| `Pages/Home.razor` | The only route: the forecast, or the setup when the query string says `vis=opsaetning`. |
| `Components/SetupForm.razor` | The setup form. |
| `Components/PriceChart.razor` | The stacked bar chart, tooltip and table view. |
| `Layout/MainLayout.razor` | Top bar, navigation and footer. |
| `wwwroot/css/app.css` | All styles, with light and dark color tokens. |
| `wwwroot/icons/`, `favicon.ico`, `manifest.webmanifest` | App icon in every size (browser tab, iOS home screen, installable web app). Rendered by `tools/make-icons.ps1`; run `powershell -File tools\make-icons.ps1` again after changing the design. |

`BlazorWebAssemblyLoadAllGlobalizationData` is on, so Danish day and month names work whatever
the browser's language is. Times follow the browser's time zone, which is correct for users in
Denmark.
