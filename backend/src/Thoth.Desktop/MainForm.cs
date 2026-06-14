using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Thoth.Desktop;

internal sealed class MainForm : Form
{
    private static readonly Uri AppUri = new("http://localhost:8091/");
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    private readonly WebView2 webView;
    private readonly Panel statusPanel;
    private readonly Label statusLabel;
    private readonly Button retryButton;
    private readonly ApiProcessHost apiHost = new();

    private CancellationTokenSource? startupCancellation;
    private bool loading;

    public MainForm()
    {
        Text = "Thoth";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 640);
        Size = new Size(1280, 800);

        webView = new WebView2
        {
            AllowExternalDrop = false,
            Dock = DockStyle.Fill,
            Visible = false
        };
        webView.NavigationCompleted += OnNavigationCompleted;

        statusLabel = new Label
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, 11),
            MaximumSize = new Size(720, 0),
            Text = "Iniciando Thoth...",
            TextAlign = ContentAlignment.MiddleCenter
        };

        retryButton = new Button
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            Padding = new Padding(14, 6, 14, 6),
            Text = "Tentar novamente",
            Visible = false
        };
        retryButton.Click += async (_, _) => await LoadApplicationAsync();

        var layout = new TableLayoutPanel
        {
            BackColor = Color.FromArgb(248, 249, 251),
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.Controls.Add(statusLabel, 0, 1);
        layout.Controls.Add(retryButton, 0, 2);

        statusPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        statusPanel.Controls.Add(layout);

        Controls.Add(webView);
        Controls.Add(statusPanel);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadApplicationAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        startupCancellation?.Cancel();
        startupCancellation?.Dispose();
        apiHost.Stop();
        base.OnFormClosing(e);
    }

    private async Task LoadApplicationAsync()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        retryButton.Visible = false;
        webView.Visible = false;
        statusPanel.Visible = true;
        statusPanel.BringToFront();

        startupCancellation?.Cancel();
        startupCancellation?.Dispose();
        startupCancellation = new CancellationTokenSource();

        try
        {
            SetStatus("Iniciando Thoth...");
            await apiHost.EnsureRunningAsync(startupCancellation.Token);
            await WaitForApiAsync(startupCancellation.Token);
            await EnsureWebViewAsync();

            SetStatus("Abrindo Thoth...");
            webView.Visible = true;
            statusPanel.Visible = false;
            webView.BringToFront();
            webView.CoreWebView2.Navigate(AppUri.ToString());
        }
        catch (OperationCanceledException) when (IsDisposed || Disposing)
        {
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowError(
                "Microsoft Edge WebView2 Runtime nao foi encontrado.\r\n\r\n" +
                "Reinstale o Thoth ou instale o WebView2 Runtime e tente novamente.");
        }
        catch (Exception ex)
        {
            ShowError(
                "Nao foi possivel abrir o Thoth em http://localhost:8091.\r\n\r\n" +
                ex.Message);
        }
        finally
        {
            loading = false;
        }
    }

    private async Task WaitForApiAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 60;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetStatus($"Iniciando Thoth em http://localhost:8091... ({attempt}/{maxAttempts})");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, AppUri);
                using var response = await Http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if ((int)response.StatusCode < 500)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException("A API do Thoth nao respondeu dentro do tempo esperado.");
    }

    private async Task EnsureWebViewAsync()
    {
        if (webView.CoreWebView2 is not null)
        {
            return;
        }

        SetStatus("Inicializando WebView2...");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userDataFolder = Path.Combine(localAppData, "Thoth", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await webView.EnsureCoreWebView2Async(environment);

        var coreWebView = webView.CoreWebView2
            ?? throw new InvalidOperationException("WebView2 nao foi inicializado.");

        coreWebView.Settings.IsZoomControlEnabled = true;
        coreWebView.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
        };
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            return;
        }

        ShowError($"Nao foi possivel carregar o Thoth. Status do WebView2: {e.WebErrorStatus}.");
    }

    private void SetStatus(string message)
    {
        statusLabel.Text = message;
    }

    private void ShowError(string message)
    {
        statusLabel.Text = message;
        retryButton.Visible = true;
        webView.Visible = false;
        statusPanel.Visible = true;
        statusPanel.BringToFront();
    }
}
