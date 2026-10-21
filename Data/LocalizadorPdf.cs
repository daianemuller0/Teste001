namespace HowdenExtrator.Poc.Data;

public sealed class ErroProposta(string mensagem) : Exception(mensagem);

public static class LocalizadorPdf
{
    /// <summary>Acha o único PDF de &lt;base&gt;\&lt;AF&gt;\&lt;subpasta&gt;. Só lista a pasta — não altera nada.</summary>
    public static string Localizar(string pastaBase, string af, string subPasta)
    {
        var partes = subPasta.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        var pasta = Path.Combine([pastaBase, af, .. partes]);

        if (!Directory.Exists(pasta))
            throw new ErroProposta($"Pasta não encontrada: {pasta}");

        var pdfs = Directory.GetFiles(pasta, "*.pdf", SearchOption.TopDirectoryOnly);
        return pdfs.Length switch
        {
            0 => throw new ErroProposta($"Nenhum PDF em {pasta}"),
            1 => pdfs[0],
            _ => throw new ErroProposta($"{pdfs.Length} PDFs em {pasta} (esperado 1)"),
        };
    }
}
