using System.Text.RegularExpressions;

namespace HowdenExtrator.Poc.Data;

/// <summary>'AF3242-01' → AF "AF3242" e item 1.</summary>
public readonly record struct NumeroPedido(string Af, int Item)
{
    // AF só com letras/dígitos/_ : o valor vira nome de pasta, então nada de
    // barras ou ".." vindos da planilha.
    private static readonly Regex Padrao =
        new(@"^(?<af>\w+)\s*-\s*0*(?<item>\d+)$", RegexOptions.Compiled);

    public static bool TryParse(string? texto, out NumeroPedido pedido)
    {
        pedido = default;
        var m = Padrao.Match((texto ?? "").Trim().ToUpperInvariant());
        if (!m.Success || !int.TryParse(m.Groups["item"].Value, out var item))
            return false;
        pedido = new NumeroPedido(m.Groups["af"].Value, item);
        return true;
    }
}
