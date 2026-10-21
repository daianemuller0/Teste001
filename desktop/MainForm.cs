using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HowdenExtrator.Desktop;

/// <summary>
/// A janela do sistema: um WebView2 (Chromium embutido) ocupando tudo,
/// apontando para o servidor Kestrel que roda dentro deste mesmo processo.
/// </summary>
public sealed class MainForm : Form
{
    private readonly string _homeUrl;
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly Action<int, string> _progresso;
    private readonly Action _pronto;
    private bool _avisouPronto;

    public MainForm(string url, Action<int, string> progresso, Action pronto)
    {
        _homeUrl = url;
        _progresso = progresso;
        _pronto = pronto;

        Text = "EP · Extrator de Propostas — Howden";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1440, 900);
        MinimumSize = new Size(1000, 640);
        WindowState = FormWindowState.Maximized;

        var menu = new MenuStrip();
        var inicio = new ToolStripMenuItem("Início");
        inicio.Click += (_, _) => _webView.CoreWebView2?.Navigate(_homeUrl);
        var recarregar = new ToolStripMenuItem("Recarregar") { ShortcutKeys = Keys.F5, ShowShortcutKeys = true };
        recarregar.Click += (_, _) => _webView.CoreWebView2?.Reload();
        menu.Items.Add(inicio);
        menu.Items.Add(recarregar);
        MainMenuStrip = menu;

        Controls.Add(_webView);   // Fill primeiro
        Controls.Add(menu);       // Top depois

        Load += MainForm_Load;
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        try
        {
            _progresso(70, "Preparando o navegador embutido…");

            // Fixed Version (pasta WebView2Runtime ao lado do .exe) quando
            // existir; senão o Evergreen do Windows/Edge.
            var pastaFixa = Path.Combine(AppContext.BaseDirectory, "WebView2Runtime");
            var pastaDados = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HowdenExtrator", "WebView2UserData");
            var ambiente = await CoreWebView2Environment.CreateAsync(
                Directory.Exists(pastaFixa) ? pastaFixa : null, pastaDados);

            await _webView.EnsureCoreWebView2Async(ambiente);

            _progresso(85, "Carregando o sistema…");
            _webView.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                if (_avisouPronto) return;
                _avisouPronto = true;
                _progresso(100, "Pronto!");
                _pronto();
            };
            _webView.CoreWebView2.Navigate(_homeUrl);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(this,
                "Esta máquina não tem o WebView2 Runtime (vem com o Windows 10/11 atualizado ou o Edge).\n\n" +
                "Peça ao TI para instalar o \"Microsoft Edge WebView2 Runtime (Evergreen)\" — download em:\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/",
                "EP — componente faltando", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não consegui abrir a janela do sistema:\n\n" + ex.Message,
                "EP", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }
}
