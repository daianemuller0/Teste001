using HowdenExtrator.Poc.Components;
using HowdenExtrator.Poc.Data;

namespace HowdenExtrator.Poc;

/// <summary>
/// Fábrica do servidor do sistema (Kestrel + Blazor Server). Usada em dois modos:
///  - navegador (Program.cs deste projeto): roda como site normal;
///  - desktop (HowdenExtrator.Desktop): o MESMO servidor sobe dentro do
///    processo da janela WinForms e é exibido num WebView2.
/// Não há login nem banco de dados: o programa só lê a planilha enviada e os
/// PDFs da pasta dos pedidos.
/// </summary>
public static class BackendHost
{
    public static WebApplication CreateApp(string[] args, string[]? urls = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Sem isso o modo desktop abre sem CSS/JS (ver servicos/BackendHost.cs).
        builder.WebHost.UseStaticWebAssets();

        if (urls is { Length: > 0 })
            builder.WebHost.UseUrls(urls);
        else if (string.IsNullOrEmpty(builder.Configuration["urls"]) &&
                 string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
            builder.WebHost.UseUrls("http://localhost:5083");

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.Configure<ExtracaoOpcoes>(builder.Configuration.GetSection("Extracao"));
        builder.Services.AddScoped<ProcessadorPlanilha>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler("/Error", createScopeForErrors: true);

        // Modo desktop: o wwwroot sai em /_content/HowdenExtrator.Poc/… — atende de lá.
        var webRoot = app.Environment.WebRootFileProvider;
        app.Use((ctx, next) =>
        {
            var caminho = ctx.Request.Path.Value;
            if (!string.IsNullOrEmpty(caminho) && caminho.Contains('.') &&
                !caminho.StartsWith("/_") && !webRoot.GetFileInfo(caminho).Exists)
            {
                var alternativo = "/_content/HowdenExtrator.Poc" + caminho;
                if (webRoot.GetFileInfo(alternativo).Exists)
                    ctx.Request.Path = alternativo;
            }
            return next(ctx);
        });
        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}
