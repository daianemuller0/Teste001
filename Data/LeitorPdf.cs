using System.Text;
using UglyToad.PdfPig;

namespace HowdenExtrator.Poc.Data;

public static class LeitorPdf
{
    /// <summary>
    /// Texto do PDF, uma linha de texto por linha visual (palavras na mesma
    /// altura, da esquerda para a direita). Abre o arquivo SOMENTE para leitura
    /// e deixa outros programas abrirem o mesmo arquivo ao mesmo tempo.
    /// </summary>
    public static string LerTexto(string caminho)
    {
        using var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return LerTexto(fs);
    }

    public static string LerTexto(Stream pdf)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(pdf);
        foreach (var pagina in doc.GetPages())
        {
            var linhas = pagina.GetWords()
                .GroupBy(p => (int)Math.Round(p.BoundingBox.Bottom / 3.0))   // mesma altura ≈ mesma linha
                .OrderByDescending(g => g.Key);                              // PDF: Y cresce para cima
            foreach (var linha in linhas)
                sb.AppendLine(string.Join(" ", linha.OrderBy(p => p.BoundingBox.Left).Select(p => p.Text)));
        }
        return sb.ToString();
    }
}
