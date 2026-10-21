using System.Text.RegularExpressions;

namespace HowdenExtrator.Poc.Data;

public sealed class ErroProposta(string mensagem) : Exception(mensagem);

public static class LocalizadorPdf
{
    private static readonly Regex CoringaPedido =
        new(@"(?<=^|[\\/])x{3,4}(?=[\\/]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Acha o único PDF de &lt;base&gt;\&lt;AF&gt;\&lt;subpasta&gt;. Só lista a pasta — não altera nada.</summary>
    public static string Localizar(string pastaBase, string af, string subPasta)
    {
        string pasta;
        if (CoringaPedido.IsMatch(pastaBase))
        {
            // Caminho completo com "xxxx" no lugar do pedido: H:\PV_AFTERMARKET\xxxx\COMERCIAL\Proposta Comercial Valida
            pasta = CoringaPedido.Replace(pastaBase, af);
        }
        else
        {
            var partes = subPasta.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            pasta = Path.Combine([pastaBase, af, .. partes]);
        }

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
