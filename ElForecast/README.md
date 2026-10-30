# ElForecast

A Blazor WebAssembly app (.NET 10) that shows Danish household electricity prices hour by
hour for about a week ahead. It uses published prices where they exist and the
stromligning.dk spot price forecast for the rest. The UI is in Danish.

It is built on the [`Stromligning`](../Stromligning/README.md) library, the same library the
StromPriser WinForms app uses. The library runs unchanged in the browser: the API sends
`Access-Control-Allow-Origin: *`, so the app calls it directly with no backend.

## Running

```bash
dotnet run --project ElForecast/ElForecast.csproj
```

Then open <http://localhost:5276>. To publish as static files (for GitHub Pages, Azure Static Web
Apps, any web server), run `dotnet publish ElForecast -c Release` and serve
`bin/Release/net10.0/publish/wwwroot`.

## Pages

| Route | Page |
|---|---|
| `/` | **Prognose**: summary tiles (cheapest hour, priciest hour, average, forecast age) and the chart. |
| `/opsaetning` | **Opsætning**: grid company, customer group, trading company, product and *incl./excl. VAT*. |

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
  total incl. VAT.
- *Vis som tabel* shows the same data as a table.
- A negative spot price is drawn below the zero line.

The forecast rows are estimated as described in the
[library README](../Stromligning/README.md#how-forecast-rows-are-estimated).

## Saved settings

The selections are stored as JSON under the key `elforecast.setup` in the browser's
`localStorage`. Without a saved grid company, the front page links to the setup page. A saved
company or product that no longer exists is replaced by the default. If storage is blocked,
the settings only last until the page is closed.

## Source files

| File | Responsibility |
|---|---|
| `Program.cs` | Sets the da-DK culture and registers the library's client and `PriceService`. |
| `AppInfo.cs` | Version and copyright from the assembly, shown in the footer. Set `Version` in the `.csproj`. |
| `Services/ForecastSetup.cs` | The saved selections. |
| `Services/SetupStore.cs` | Reads and writes them in `localStorage` through `wwwroot/js/elforecast.js`. |
| `Services/CatalogService.cs` | Fetches the grid and trading companies once per visit, sorted the Danish way. |
| `Components/PriceChart.razor` | The stacked bar chart, tooltip and table view. |
| `Pages/Home.razor`, `Pages/Setup.razor` | The two pages. |
| `Layout/MainLayout.razor` | Top bar, navigation and footer. |
| `wwwroot/css/app.css` | All styles, with light and dark color tokens. |
| `wwwroot/icons/`, `favicon.ico`, `manifest.webmanifest` | App icon in every size (browser tab, iOS home screen, installable web app). Rendered by `tools/make-icons.ps1`. Run `powershell -File tools\make-icons.ps1` again after changing the design. |

`BlazorWebAssemblyLoadAllGlobalizationData` is on, so Danish day and month names work whatever
the browser's language is. Times follow the browser's time zone, which is correct for users in
Denmark.
