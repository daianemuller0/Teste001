using System.Text.RegularExpressions;
using HowdenExtrator.Poc.Models;

namespace HowdenExtrator.Poc.Data;

/// <summary>
/// Lê os dados do item da proposta a partir do texto do PDF. Cada campo é
/// procurado pelo seu rótulo ("Model", "Serial Number"…), um independente do
/// outro — por isso a ordem dos campos no PDF não importa.
/// </summary>
public static class ExtratorProposta
{
    public static readonly IReadOnlyDictionary<string, string> RotulosPadrao = new Dictionary<string, string>
    {
        ["modelo"] = @"(?:Model(?:o)?(?:\s*do\s*Equipamento)?|Equipment\s*Model)",
        ["serial"] = @"(?:Serial\s*(?:Number|No\.?|N[ºo°.]?)?|S/N|N[ºo°.]?\s*de\s*S[ée]rie|N[úu]mero\s*de\s*S[ée]rie)",
        ["partNumber"] = @"(?:Part\s*(?:Number|No\.?|N[ºo°.]?)|P/N|PN)",
        ["precoUnitario"] = @"(?:Unit(?:ary)?\s*Price|Pre[çc]o\s*Unit[áa]rio|Valor\s*Unit[áa]rio)",
        ["precoTotal"] = @"(?:Total\s*Price|Pre[çc]o\s*Total|Valor\s*Total|Total\s*Amount)",
        ["prazo"] = @"(?:Lead\s*Time|Prazo(?:\s*de\s*Entrega)?|Delivery(?:\s*Time)?)",
    };

    private static readonly Regex Dinheiro =
        new(@"(?:R\$|US\$|\$|€|EUR|USD|BRL)?\s*\d[\d.,]*", RegexOptions.Compiled);

    // Início de um item: "Item 02", "2 Compressor", "01 - ...", "1)". Não pega
    // preços ("1.250,00": o número é seguido de ponto + dígito).
    private static readonly Regex InicioItem = new(
        @"^\s*(?:(?:item|pos(?:i[çc][ãa]o)?)\s*0*(?<n>\d{1,3})\b|0*(?<n>\d{1,3})(?=\s+[A-Za-zÀ-ÿ]|\s*[.):\-](?!\d)))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Recorta o trecho do item; sem marcações de item, devolve o texto todo.</summary>
    public static (string Bloco, bool Achou) BlocoDoItem(string texto, int item)
    {
        var linhas = texto.Split('\n');
        var marcas = new List<(int Linha, int N)>();
        for (var i = 0; i < linhas.Length; i++)
        {
            var m = InicioItem.Match(linhas[i]);
            if (m.Success) marcas.Add((i, int.Parse(m.Groups["n"].Value)));
        }

        var iIni = marcas.FindIndex(x => x.N == item);
        if (iIni < 0) return (texto, false);
        var ini = marcas[iIni].Linha;

        // O item acaba no próximo item de número MAIOR (um "2 UN" de quantidade dentro do item 3 não corta).
        var iFim = marcas.FindIndex(iIni + 1, x => x.N > item);
        var ate = iFim < 0 ? linhas.Length : marcas[iFim].Linha;
        return (string.Join('\n', linhas[ini..ate]), true);
    }

    public static (DadosProposta Dados, bool ItemAchado) Extrair(
        string texto, int item, IReadOnlyDictionary<string, string>? sobrescritos = null)
    {
        var (bloco, achou) = BlocoDoItem(texto, item);
        var linhas = bloco.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

        string Campo(string chave, bool dinheiro = false)
        {
            var rotulo = sobrescritos is not null && sobrescritos.TryGetValue(chave, out var r) && !string.IsNullOrWhiteSpace(r)
                ? r : RotulosPadrao[chave];
            return ValorApos(linhas, rotulo, dinheiro);
        }

        var dados = new DadosProposta(
            Campo("modelo"), Campo("serial"), Campo("partNumber"),
            Campo("precoUnitario", true), Campo("precoTotal", true), Campo("prazo"));
        return (dados, achou);
    }

    /// <summary>Nomes dos campos que ficaram vazios.</summary>
    public static IEnumerable<string> Faltando(DadosProposta d)
    {
        if (d.Modelo == "") yield return "modelo";
        if (d.Serial == "") yield return "serial";
        if (d.PartNumber == "") yield return "part number";
        if (d.PrecoUnitario == "") yield return "preço unitário";
        if (d.PrecoTotal == "") yield return "preço total";
        if (d.Prazo == "") yield return "prazo";
    }

    private static string ValorApos(string[] linhas, string rotulo, bool dinheiro)
    {
        var rx = new Regex(@"\b" + rotulo + @"\s*[:\-–]?\s*(?<v>.*)", RegexOptions.IgnoreCase);
        for (var i = 0; i < linhas.Length; i++)
        {
            var m = rx.Match(linhas[i]);
            if (!m.Success) continue;

            var resto = m.Groups["v"].Value.Trim();
            if (resto.Length == 0 && i + 1 < linhas.Length) resto = linhas[i + 1];   // valor na linha de baixo
            if (resto.Length == 0) continue;

            if (!dinheiro) return resto;
            var v = Dinheiro.Match(resto);
            if (v.Success) return v.Value.Trim();
        }
        return "";
    }
}
