using System.Globalization;
using System.Net.Http;
using Stromligning;

namespace StromPriser;

public sealed class MainForm : Form
{
    private static readonly CultureInfo Da = CultureInfo.GetCultureInfo("da-DK");

    private readonly StromligningClient _client = new(userAgent: "StromPriser/1.0");
    private readonly PriceService _service;
    private readonly AppSettings _settings = AppSettings.Load();
    private CancellationTokenSource? _inFlight;
    private PriceResult? _lastResult;

    private readonly ComboBox _cmbSupplier = NewCombo();
    private readonly ComboBox _cmbCustomerGroup = NewCombo();
    private readonly ComboBox _cmbCompany = NewCombo();
    private readonly ComboBox _cmbProduct = NewCombo();
    private readonly CheckBox _chkHourly = new()
    {
        Text = "Vis pr. time (gennemsnit af kvarterspriser)",
        Checked = true,
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };
    private readonly CheckBox _chkForecast = new()
    {
        Text = "Medtag prognose (ca. 7 dage frem, estimeret)",
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };
    private readonly Button _btnFetch = new()
    {
        Text = "Hent priser",
        Width = 130,
        Height = 30,
        Enabled = false
    };
    private readonly Button _btnSave = new()
    {
        Text = "Gem som fil...",
        Width = 120,
        Height = 30,
        Enabled = false
    };

    // AutoSize is switched off on all of these: it defaults to true, which overrides the
    // docked height and silently swallows the second line of the context text.
    private readonly Label _lblHeadline = new()
    {
        Text = "Vælg dit netselskab og tryk \"Hent priser\".",
        Font = new Font("Segoe UI", 11F, FontStyle.Regular),
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 26
    };
    private readonly Label _lblPrice = new()
    {
        Text = "-",
        Font = new Font("Segoe UI", 26F, FontStyle.Bold),
        ForeColor = Color.FromArgb(0, 110, 60),
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 54
    };
    private readonly Label _lblRange = new()
    {
        Text = "",
        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
        ForeColor = Color.FromArgb(50, 50, 50),
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 26
    };
    // Two single-line labels rather than one two-line label: a docked Label's wrapping is
    // easy to get wrong, and each line here is independently meaningful anyway.
    private readonly Label _lblContext = new()
    {
        Text = "",
        Font = new Font("Segoe UI", 9F),
        ForeColor = Color.DimGray,
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 22
    };
    private readonly Label _lblSavings = new()
    {
        Text = "",
        Font = new Font("Segoe UI", 9F),
        ForeColor = Color.DimGray,
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 22
    };

    private readonly Label _lblRowsCaption = new()
    {
        Dock = DockStyle.Top,
        Height = 22,
        ForeColor = Color.DimGray,
        Padding = new Padding(4, 4, 0, 0)
    };

    private readonly DataGridView _gridBreakdown = NewGrid();
    private readonly DataGridView _gridRows = NewGrid();
    private readonly ToolStripStatusLabel _status = new() { Text = "Klar." };

    public MainForm()
    {
        Text = "Strømpriser - laveste pris de kommende 24 timer";
        Width = 1280;
        Height = 840;
        MinimumSize = new Size(1040, 680);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        _service = new PriceService(_client);
        BuildLayout();

        _chkHourly.Checked = _settings.Hourly;
        _chkForecast.Checked = _settings.IncludeForecast;
        _lblRowsCaption.Text = RowsCaption(false);

        _cmbSupplier.SelectedIndexChanged += OnSupplierChanged;
        _cmbCompany.SelectedIndexChanged += OnCompanyChanged;
        _btnFetch.Click += async (_, _) => await LoadPricesAsync();
        _btnSave.Click += async (_, _) => await SaveResultAsync();
        Load += async (_, _) => await LoadReferenceDataAsync();
        FormClosing += (_, _) => SaveSettings();
    }

    // ---------------- layout ----------------

    private void BuildLayout()
    {
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        // Controls are added Fill-first: WinForms docks the last-added control first, so the
        // Fill control must go in first to end up with whatever space is left over.
        Controls.Add(BuildRowsPanel());
        Controls.Add(BuildSummaryPanel());
        Controls.Add(BuildSelectionBox());
        Controls.Add(statusStrip);
    }

    private GroupBox BuildSelectionBox()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            Padding = new Padding(8, 4, 8, 4)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 4; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        table.Controls.Add(NewLabel("Netselskab:"), 0, 0);
        table.Controls.Add(_cmbSupplier, 1, 0);
        table.Controls.Add(NewLabel("Kundegruppe:"), 2, 0);
        table.Controls.Add(_cmbCustomerGroup, 3, 0);

        table.Controls.Add(NewLabel("Elhandelsselskab:"), 0, 1);
        table.Controls.Add(_cmbCompany, 1, 1);
        table.Controls.Add(NewLabel("Produkt:"), 2, 1);
        table.Controls.Add(_cmbProduct, 3, 1);

        table.Controls.Add(_chkHourly, 1, 2);
        table.Controls.Add(_chkForecast, 1, 3);
        // Right-to-left so "Hent priser" sits in the corner where it always was.
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            WrapContents = false
        };
        buttons.Controls.Add(_btnFetch);
        buttons.Controls.Add(_btnSave);
        table.Controls.Add(buttons, 3, 3);

        var box = new GroupBox
        {
            Text = "Dit elselskab (netselskabet bestemmer distributionstariffen)",
            Dock = DockStyle.Top,
            Height = 188,
            Padding = new Padding(6)
        };
        box.Controls.Add(table);
        return box;
    }

    private Panel BuildSummaryPanel()
    {
        SetupBreakdownColumns();

        var text = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 8, 8) };
        // Added bottom-up: Dock=Top stacks in reverse order of addition.
        text.Controls.Add(_lblSavings);
        text.Controls.Add(_lblContext);
        text.Controls.Add(_lblRange);
        text.Controls.Add(_lblPrice);
        text.Controls.Add(_lblHeadline);

        var breakdown = new Panel
        {
            Dock = DockStyle.Right,
            Width = 520,
            Padding = new Padding(0, 8, 8, 8)
        };
        breakdown.Controls.Add(_gridBreakdown);

        var panel = new Panel { Dock = DockStyle.Top, Height = 240, BackColor = Color.White };
        panel.Controls.Add(text);
        panel.Controls.Add(breakdown);
        return panel;
    }

    private Panel BuildRowsPanel()
    {
        SetupRowColumns();

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4) };
        panel.Controls.Add(_gridRows);
        panel.Controls.Add(_lblRowsCaption);
        return panel;
    }

    private static string RowsCaption(bool withForecast) =>
        (withForecast
            ? "Kendte priser efterfulgt af prognose (kursiv: estimeret spotpris + tariffer fra samme klokkeslæt)"
            : "Alle 24 timer") +
        " - komponenter er ekskl. moms, kolonnen \"Total\" er inkl. moms (kr/kWh)";

    private void SetupBreakdownColumns()
    {
        AddColumn(_gridBreakdown, "Komponent", 210, DataGridViewContentAlignment.MiddleLeft);
        AddColumn(_gridBreakdown, "Ekskl. moms", 100, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridBreakdown, "Moms", 85, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridBreakdown, "Inkl. moms", 100, DataGridViewContentAlignment.MiddleRight);
        _gridBreakdown.ColumnHeadersVisible = true;
    }

    private void SetupRowColumns()
    {
        AddColumn(_gridRows, "Tidsrum", 165, DataGridViewContentAlignment.MiddleLeft);
        AddColumn(_gridRows, "Spotpris", 95, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridRows, "Tillæg", 85, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridRows, "Nettarif", 95, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridRows, "Systemtarif", 110, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridRows, "Distribution", 115, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridRows, "Elafgift", 90, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridRows, "Moms", 85, DataGridViewContentAlignment.MiddleRight);
        AddColumn(_gridRows, "Total", 105, DataGridViewContentAlignment.MiddleRight);
    }

    /// <summary>
    /// Grids are in Fill mode, where FillWeight - not Width - decides the proportions, so the
    /// intended width is fed in as the weight and also kept as a floor.
    /// </summary>
    private static void AddColumn(DataGridView grid, string header, int width,
        DataGridViewContentAlignment align)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            FillWeight = width,
            MinimumWidth = Math.Min(width, 70),
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = align }
        };
        column.HeaderCell.Style.Alignment = align;
        grid.Columns.Add(column);
    }

    private static Label NewLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 9, 3, 3)
    };

    private static ComboBox NewCombo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
        Margin = new Padding(3, 4, 12, 4)
    };

    private static DataGridView NewGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        EnableHeadersVisualStyles = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        // Pinned so the 7-row breakdown fits its panel instead of sprouting a scrollbar
        // that hides the "I alt" row.
        RowTemplate = { Height = 24 },
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        ColumnHeadersHeight = 30,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(238, 240, 243),
            ForeColor = Color.FromArgb(40, 40, 40),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 4, 0)
        }
    };

    // ---------------- data loading ----------------

    private async Task LoadReferenceDataAsync()
    {
        SetBusy(true, "Henter selskaber...");
        try
        {
            using var cts = new CancellationTokenSource();
            var suppliersTask = _client.GetSuppliersAsync(cts.Token);
            var companiesTask = _client.GetCompaniesAsync(cts.Token);
            await Task.WhenAll(suppliersTask, companiesTask);

            _cmbSupplier.Items.Clear();
            foreach (var supplier in suppliersTask.Result.OrderBy(s => s.Name, StringComparer.Create(Da, true)))
                _cmbSupplier.Items.Add(supplier);

            _cmbCompany.Items.Clear();
            _cmbCompany.Items.Add(NoCompany);
            foreach (var company in companiesTask.Result
                         .Where(c => c.SpotProducts.Any())
                         .OrderBy(c => c.Name, StringComparer.Create(Da, true)))
            {
                _cmbCompany.Items.Add(company);
            }

            _cmbCompany.SelectedIndex = 0;
            RestoreSelection();
            _status.Text = $"{_cmbSupplier.Items.Count} netselskaber og " +
                           $"{_cmbCompany.Items.Count - 1} elhandelsselskaber hentet.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or StromligningApiException)
        {
            ShowError("Kunne ikke hente selskabslisterne.", ex);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private static readonly Company NoCompany = new() { Name = "(intet - kun spotpris uden tillæg)" };

    /// <summary>
    /// Re-selects last session's choices. Order matters: picking a supplier or company fills
    /// the dependent combo with its defaults, which the saved value then overrides.
    /// </summary>
    private void RestoreSelection()
    {
        SelectWhere<Supplier>(_cmbSupplier, s => s.Id == _settings.SupplierId);
        SelectWhere<CustomerGroup>(_cmbCustomerGroup, g => g.Id == _settings.CustomerGroupId);
        SelectWhere<Company>(_cmbCompany, c => c.Name == _settings.CompanyName);
        SelectWhere<Product>(_cmbProduct, p => p.Id == _settings.ProductId);
    }

    private static void SelectWhere<T>(ComboBox combo, Func<T, bool> match)
    {
        var item = combo.Items.OfType<T>().FirstOrDefault(match);
        if (item is not null)
            combo.SelectedItem = item;
    }

    private void SaveSettings()
    {
        // Before the lists have loaded there is nothing selected yet, and saving would wipe
        // out the previous session's choices.
        if (_cmbSupplier.Items.Count > 0)
        {
            _settings.SupplierId = (_cmbSupplier.SelectedItem as Supplier)?.Id;
            _settings.CustomerGroupId = (_cmbCustomerGroup.SelectedItem as CustomerGroup)?.Id;
            _settings.CompanyName = _cmbCompany.SelectedItem is Company c && c != NoCompany ? c.Name : null;
            _settings.ProductId = (_cmbProduct.SelectedItem as Product)?.Id;
        }

        _settings.Hourly = _chkHourly.Checked;
        _settings.IncludeForecast = _chkForecast.Checked;
        _settings.Save();
    }

    private void OnSupplierChanged(object? sender, EventArgs e)
    {
        _cmbCustomerGroup.Items.Clear();

        if (_cmbSupplier.SelectedItem is not Supplier supplier)
        {
            _btnFetch.Enabled = false;
            return;
        }

        foreach (var group in supplier.CustomerGroups)
            _cmbCustomerGroup.Items.Add(group);

        if (_cmbCustomerGroup.Items.Count > 0)
        {
            var defaultIndex = supplier.CustomerGroups.FindIndex(g => g.Default);
            _cmbCustomerGroup.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
        }

        _btnFetch.Enabled = true;
    }

    private void OnCompanyChanged(object? sender, EventArgs e)
    {
        _cmbProduct.Items.Clear();

        if (_cmbCompany.SelectedItem is not Company company)
            return;

        foreach (var product in company.SpotProducts)
            _cmbProduct.Items.Add(product);

        if (_cmbProduct.Items.Count > 0)
            _cmbProduct.SelectedIndex = 0;
    }

    private async Task LoadPricesAsync()
    {
        if (_cmbSupplier.SelectedItem is not Supplier supplier)
            return;

        SaveSettings();

        _inFlight?.Cancel();
        _inFlight?.Dispose();
        _inFlight = new CancellationTokenSource();
        var token = _inFlight.Token;

        SetBusy(true, "Henter priser...");
        try
        {
            var query = new PriceQuery(
                supplier.Id,
                (_cmbCustomerGroup.SelectedItem as CustomerGroup)?.Id,
                (_cmbProduct.SelectedItem as Product)?.Id)
            {
                Hourly = _chkHourly.Checked,
                IncludeForecast = _chkForecast.Checked
            };

            var result = await _service.GetPricesAsync(query, token);
            if (token.IsCancellationRequested)
                return;

            _lastResult = result;
            Render(result);
            if (result.ForecastError is not null)
                _status.Text += $"  Prognosen kunne ikke hentes: {result.ForecastError.Message}";
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request - nothing to report.
        }
        catch (Exception ex) when (ex is HttpRequestException or StromligningApiException)
        {
            ShowError("Kunne ikke hente priser.", ex);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                SetBusy(false, null);
        }
    }

    // ---------------- rendering ----------------

    private void Render(PriceResult result)
    {
        _gridRows.Rows.Clear();
        _gridBreakdown.Rows.Clear();

        var rows = result.Rows;
        if (result.Summary is not { } summary)
        {
            _lblHeadline.Text = "Ingen priser tilgængelige for det valgte selskab.";
            _lblPrice.Text = "-";
            _lblRange.Text = "";
            _lblContext.Text = "";
            _lblSavings.Text = "";
            _status.Text = "Ingen data.";
            return;
        }

        var cheapest = summary.Cheapest;
        var priciest = summary.Priciest;
        var forecastCount = result.ForecastCount;

        _lblHeadline.Text = forecastCount > 0
            ? $"Laveste pris frem til {rows[^1].End.ToString(@"ddd dd'/'MM HH':'mm", Da)} (inkl. prognose)"
            : "Laveste pris i de kommende 24 timer";
        _lblPrice.Text = $"{cheapest.Total.Total.ToString("N4", Da)} kr/kWh";
        _lblRange.Text = FormatRange(cheapest) + (cheapest.IsForecast ? "  (prognose - estimeret)" : "");
        _lblRowsCaption.Text = RowsCaption(forecastCount > 0);
        _lblContext.Text =
            $"Dyreste: {priciest.Total.Total.ToString("N4", Da)} kr/kWh ({FormatRange(priciest)})" +
            $"     Gennemsnit: {summary.AverageTotal.ToString("N4", Da)} kr/kWh";
        _lblSavings.Text =
            $"Du sparer {summary.Saving.ToString("N4", Da)} kr/kWh " +
            $"({summary.SavingFraction.ToString("P0", Da)}) ved at bruge " +
            $"strømmen i den billigste periode frem for den dyreste.";

        FillBreakdown(cheapest);

        foreach (var row in rows)
        {
            var index = _gridRows.Rows.Add(
                FormatRange(row),
                row.Electricity.ExVat.ToString("N4", Da),
                row.Surcharge.ExVat.ToString("N4", Da),
                row.NetTariff.ExVat.ToString("N4", Da),
                row.SystemTariff.ExVat.ToString("N4", Da),
                row.Distribution.ExVat.ToString("N4", Da),
                row.ElectricityTax.ExVat.ToString("N4", Da),
                row.Total.Vat.ToString("N4", Da),
                row.Total.Total.ToString("N4", Da));

            var cell = _gridRows.Rows[index];
            var fontStyle = row.IsForecast ? FontStyle.Italic : FontStyle.Regular;
            if (row.IsForecast)
                cell.DefaultCellStyle.ForeColor = Color.FromArgb(90, 90, 110);

            if (ReferenceEquals(row, cheapest))
            {
                cell.DefaultCellStyle.BackColor = Color.FromArgb(210, 244, 220);
                fontStyle |= FontStyle.Bold;
            }
            else if (ReferenceEquals(row, priciest))
            {
                cell.DefaultCellStyle.BackColor = Color.FromArgb(253, 224, 224);
            }

            if (fontStyle != FontStyle.Regular)
                cell.DefaultCellStyle.Font = new Font(_gridRows.Font, fontStyle);
        }

        var forecastText = forecastCount > 0 ? $" ({forecastCount} fra prognose)" : "";
        _status.Text = $"{rows.Count} perioder{forecastText} i prisområde {result.PriceArea ?? "?"} - " +
                       $"opdateret {DateTime.Now.ToString("HH:mm:ss", Da)}.";
    }

    private void FillBreakdown(PriceRow row)
    {
        AddBreakdownRow("Spotpris (rå elpris)", row.Electricity);
        AddBreakdownRow("Tillæg (elhandel)", row.Surcharge);
        AddBreakdownRow("Transmission – nettarif", row.NetTariff);
        AddBreakdownRow("Transmission – systemtarif", row.SystemTariff);
        AddBreakdownRow("Distribution (netselskab)", row.Distribution);
        AddBreakdownRow("Elafgift", row.ElectricityTax);

        var total = _gridBreakdown.Rows.Add(
            "I alt",
            row.Total.ExVat.ToString("N4", Da),
            row.Total.Vat.ToString("N4", Da),
            row.Total.Total.ToString("N4", Da));

        var style = _gridBreakdown.Rows[total].DefaultCellStyle;
        style.Font = new Font(_gridBreakdown.Font, FontStyle.Bold);
        style.BackColor = Color.FromArgb(240, 240, 240);
    }

    private void AddBreakdownRow(string label, Money money) =>
        _gridBreakdown.Rows.Add(
            label,
            money.ExVat.ToString("N4", Da),
            money.Vat.ToString("N4", Da),
            money.Total.ToString("N4", Da));

    // Separators are escaped: in a custom format string "/" and ":" mean the culture's
    // separators, which in da-DK are "-" and ".", giving ranges like "15-08 13.00-14.00".
    private static string FormatRange(PriceRow row) =>
        $"{row.Start.ToString(@"ddd dd'/'MM HH':'mm", Da)}–{row.End.ToString(@"HH':'mm", Da)}";

    // ---------------- export ----------------

    private async Task SaveResultAsync()
    {
        if (_lastResult is not { } result)
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Gem priser",
            Filter = "CSV-fil (*.csv)|*.csv|JSON-fil (*.json)|*.json",
            FileName = $"strompriser-{result.From.ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture)}",
            InitialDirectory = _settings.ExportFolder ?? "",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        // The dialog's filter decides the format, so a name typed without an extension
        // still gets the one the user picked.
        var format = dialog.FilterIndex == 2 ? ExportFormat.Json : ExportFormat.Csv;
        try
        {
            // da-DK so the CSV opens straight into columns in a Danish Excel.
            await PriceExporter.SaveAsync(result, dialog.FileName, format, Da);
            _settings.ExportFolder = Path.GetDirectoryName(dialog.FileName);
            _status.Text = $"Gemt: {dialog.FileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("Kunne ikke gemme filen.", ex);
        }
    }

    // ---------------- helpers ----------------

    private void SetBusy(bool busy, string? message)
    {
        UseWaitCursor = busy;
        _btnFetch.Enabled = !busy && _cmbSupplier.SelectedItem is Supplier;
        _btnSave.Enabled = !busy && _lastResult is { Rows.Count: > 0 };
        _cmbSupplier.Enabled = !busy;
        _cmbCustomerGroup.Enabled = !busy;
        _cmbCompany.Enabled = !busy;
        _cmbProduct.Enabled = !busy;

        if (message is not null)
            _status.Text = message;
    }

    private void ShowError(string context, Exception ex)
    {
        _status.Text = context;
        MessageBox.Show(this, $"{context}{Environment.NewLine}{Environment.NewLine}{ex.Message}",
            "Fejl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inFlight?.Cancel();
            _inFlight?.Dispose();
            _client.Dispose();
        }
        base.Dispose(disposing);
    }
}
