# StromPriser (Windows Forms app)

A Windows Forms app (`net10.0-windows`) that shows Danish household electricity prices for the
coming 24 hours, broken down into every component of the bill, and points out the cheapest
period. Optionally it extends the view about a week ahead using a spot price forecast, and it
can save what it shows as CSV or JSON. The UI is in Danish.

The app only holds the UI and the saved settings. Fetching, the forecast logic and the export
all come from the [Stromligning library](../Stromligning/README.md).

## Running

```bash
dotnet run --project StromPriser/StromPriser.csproj
```

Requires the .NET 10 SDK on Windows. Open `StromPriser.slnx` in Visual Studio for all projects.

## Releases and updates

Every push to `main` that touches `StromPriser/`, `Stromligning/` or the release script runs
[`.github/workflows/release-windows.yml`](../.github/workflows/release-windows.yml), which runs
[`tools/BuildRelease.ps1`](../tools/BuildRelease.ps1): it publishes the app framework-dependent
for win-x64, packs it with [Velopack](https://velopack.io) and uploads a GitHub release
(`StromPriser-win-Setup.exe`, a portable zip and the update packages).

- **Version**: `Major.Minor` is `MajorMinor` in `StromPriser.csproj`; the patch is one higher
  than the latest release with that `Major.Minor`. Local builds are `x.y.0`.
- **Locally**: `.\tools\BuildRelease.ps1 -LocalOnly` builds and packs into `.\Releases`
  without uploading (needs `dotnet tool install -g vpk --version 1.3.39-g8d2844f`).
- **Updating**: *Søg efter opdatering* checks the GitHub releases and installs a newer version,
  then restarts. It only works in a copy installed with Setup.exe.
- **Icon**: `app.ico` is drawn by `tools/make-icons.ps1`, the same artwork as ElForecast.

## Controls

| Control | Meaning |
|---|---|
| **Netselskab** | Grid company. Determines the distribution tariff and the price area (DK1/DK2). Required. |
| **Kundegruppe** | Customer group within the grid company (e.g. *C - Privatkunder*). Defaults to the company's default group. |
| **Elhandelsselskab** | Trading company. Determines the surcharge (*tillæg*). "(intet …)" means raw spot price with no surcharge. Only companies with at least one non-fixed-price product are listed. |
| **Produkt** | The trading company's product. Fixed-price products are hidden. |
| **Vis pr. time** | Average 15-minute prices into one row per clock hour. |
| **Medtag prognose** | Append forecast-based estimates for roughly 7 days ahead. |

**Hent priser** fills three areas:

- **Summary panel**: the lowest total price (incl. VAT) and when it occurs, the most expensive
  period, the average, and the saving from using the cheapest period instead of the priciest.
- **Breakdown grid**: the cheapest period split into spot price, surcharge, net tariff,
  system tariff, distribution and electricity tax, each ex. VAT, VAT and incl. VAT.
- **Rows grid**: every period in the window. Components are ex. VAT, the *Total* column is
  incl. VAT. The cheapest row is green and bold, the most expensive red.

Before roughly 13:00, tomorrow's prices are not published yet, so without the forecast the
list ends at midnight.

## Forecast rows

With **Medtag prognose** ticked, the library fills the hours after the last published price
with estimates (see [how forecast rows are estimated](../Stromligning/README.md#how-forecast-rows-are-estimated)).
In the app:

- Forecast rows are shown in grey italics.
- The headline changes to *"Laveste pris frem til … (inkl. prognose)"*.
- The status bar reports how many rows are forecast.
- If the forecast request fails, the published prices are still shown and the error is
  appended to the status bar.

## Saving to a file

**Gem som fil...** (enabled once prices are shown) saves exactly what is on screen, forecast
rows included. The format follows the file type picked in the dialog:

- **CSV**: written with the da-DK culture, so `;` between fields and decimal commas. It opens
  straight into columns in a Danish Excel.
- **JSON**: the rows with every component ex. VAT / VAT / total, plus the selection, the
  window, the summary and when the forecast was generated.

Both formats are described in the [library README](../Stromligning/README.md#priceexporter).

## Saved settings

The four selections, both check boxes and the last export folder are stored in
`%APPDATA%\StromPriser\settings.json`. They are saved when **Hent priser** is pressed and when
the window closes, and restored once the company lists have loaded at startup.

- Trading companies have no id in the API, so they are remembered by **name**.
- A missing or corrupt file means starting from the defaults. Write failures are ignored.
- Closing the window before the lists have loaded keeps the previous selections instead of
  wiping them.

## Source files

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point; runs Velopack's install hooks and starts `MainForm`. |
| `MainForm.cs` | All UI: layout, loading the company lists, building a `PriceQuery`, rendering the `PriceResult`, saving to file, saving/restoring selections, checking for updates. |
| `app.ico` | The app icon, generated by `tools/make-icons.ps1`. |
| `AppSettings.cs` | Loading and saving the user's last selections. |

## Implementation notes

- **Cancellation**: pressing *Hent priser* again cancels the request in flight.
- **Formatting**: numbers use `da-DK`. Date format strings escape `/` and `:` because in
  `da-DK` they would otherwise render as `-` and `.`.
- **Layout**: docked controls are added in reverse order (WinForms docks the last-added
  first), and docked labels have `AutoSize = false` so their heights are respected.
- **User-Agent**: the app identifies itself as `StromPriser/1.0` through the library's
  `userAgent` parameter.
