using HowdenExtrator.Poc;

// Modo navegador: o mesmo servidor do modo desktop (BackendHost), rodando
// como site normal — dotnet run, testes etc.
var app = BackendHost.CreateApp(args);

// Modo por-usuário: abre o navegador sozinho ao iniciar.
if (app.Configuration.GetValue("OpenBrowser", true))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            var url = (app.Urls.FirstOrDefault() ?? "http://localhost:5083")
                .Replace("0.0.0.0", "localhost").Replace("[::]", "localhost");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* sem navegador disponível: apenas ignora */ }
    });
}

app.Run();
