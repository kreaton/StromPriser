# StromPriser

A small Windows Forms app (.NET 10) that shows Danish household electricity prices for the
coming 24 hours, broken down into every component of the bill, and points out the cheapest
period. Optionally it extends the view about a week ahead using a spot price forecast, and it
can save what it shows as CSV or JSON.

All data comes from the public API at <https://stromligning.dk/api>. The UI is in Danish.

## Repository layout

| Folder | Project |
|---|---|
| [`Stromligning/`](Stromligning/README.md) | **Class library** (`net10.0`, no UI). API client, price/forecast logic, CSV/JSON export. Reusable elsewhere – see its README. |
| `StromPriser/` | **WinForms app** (`net10.0-windows`). UI and saved settings only; everything else comes from the library. |

## Running

```bash
dotnet run --project StromPriser/StromPriser.csproj
```

Requires the .NET 10 SDK on Windows. Open `StromPriser.slnx` in Visual Studio for both projects.

## Using the app

The selection box at the top has four drop-downs and two options:

| Control | Meaning |
|---|---|
| **Netselskab** | Grid company. Determines the distribution tariff and the price area (DK1/DK2). Required. |
| **Kundegruppe** | Customer group within the grid company (e.g. *C - Privatkunder*). Defaults to the company's default group. |
| **Elhandelsselskab** | Trading company. Determines the surcharge (*tillæg*). "(intet …)" means raw spot price with no surcharge. Only companies with at least one non-fixed-price product are listed. |
| **Produkt** | The trading company's product. Fixed-price products are hidden. |
| **Vis pr. time** | Average 15-minute prices into one row per clock hour. |
| **Medtag prognose** | Append forecast-based estimates for roughly 7 days ahead (see below). |

Press **Hent priser** to fetch. The result shows:

- **Summary panel** – the lowest total price (incl. VAT) and when it occurs, the most expensive
  period, the average, and how much is saved by using the cheapest period instead of the
  priciest one.
- **Breakdown grid** – the cheapest period split into spot price, surcharge, net tariff,
  system tariff, distribution and electricity tax, each ex. VAT, VAT and incl. VAT.
- **Rows grid** – every period in the window. Components are ex. VAT, the *Total* column is
  incl. VAT. The cheapest row is green and bold, the most expensive red.

Before roughly 13:00, tomorrow's prices are not published yet, so without the forecast the
list ends at midnight.

## Forecast

With **Medtag prognose** ticked, the hours after the last published price are filled with
estimates: the forecast spot price plus 25 % VAT, with every tariff, the surcharge and the
electricity tax copied from the latest published price at the same hour of day. Published
prices always win.

Forecast rows are shown in grey italics, the headline changes to *"Laveste pris frem til …
(inkl. prognose)"*, and the status bar reports how many rows are forecast. If the forecast
request fails, the published prices are still shown and the error is appended to the status
bar. The estimates ignore weekday/weekend and seasonal tariff differences – details in the
[library README](Stromligning/README.md#how-forecast-rows-are-estimated).

## Saving to a file

**Gem som fil...** (enabled once prices are shown) saves exactly what is on screen, forecast
rows included:

- **CSV** – one line per period, written with `;` and decimal commas (da-DK) so it opens
  straight into columns in a Danish Excel.
- **JSON** – the rows with every component ex. VAT / VAT / total, plus the selection, the
  window, the summary and when the forecast was generated.

The format follows the file type picked in the dialog. Both formats are described in the
[library README](Stromligning/README.md#priceexporter).

## Saved settings

The four selections, both check boxes and the last export folder are stored in
`%APPDATA%\StromPriser\settings.json` when **Hent priser** is pressed and when the window
closes, and restored once the company lists have loaded at startup.

- Trading companies have no id in the API, so they are remembered by **name**.
- A missing or corrupt file just means starting from defaults; write failures are ignored.
- Closing the window before the lists have loaded keeps the previous selections instead of
  wiping them.

## App source files

All in `StromPriser/`; for the library's files see [its README](Stromligning/README.md#source-files).

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point; starts `MainForm`. |
| `MainForm.cs` | All UI: layout, loading the company lists, building a `PriceQuery`, rendering the `PriceResult`, saving to file, saving/restoring selections. |
| `AppSettings.cs` | Loading and saving the user's last selections. |

## Implementation notes

- **Cancellation** – pressing *Hent priser* again cancels the request in flight.
- **Formatting** – numbers use `da-DK`; date format strings escape `/` and `:` because in
  `da-DK` they would otherwise render as `-` and `.`.
- **Layout** – docked controls are added in reverse order (WinForms docks the last-added
  first), and docked labels have `AutoSize = false` so their heights are respected.
- **User-Agent** – the app identifies itself as `StromPriser/1.0` via the library's
  `userAgent` parameter.
