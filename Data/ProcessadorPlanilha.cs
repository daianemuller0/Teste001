using ClosedXML.Excel;
using HowdenExtrator.Poc.Models;
using Microsoft.Extensions.Options;

namespace HowdenExtrator.Poc.Data;

/// <summary>
/// Percorre a coluna K da planilha, lê a proposta de cada pedido e grava nas
/// colunas L–Q. Trabalha sobre a planilha recebida (uma cópia em memória): os
/// PDFs da pasta dos pedidos só são lidos.
/// </summary>
public sealed class ProcessadorPlanilha(IOptions<ExtracaoOpcoes> opcoes)
{
    private const int ColPedido = 11;   // K
    private const int ColModelo = 12;   // L … Q = 17

    public ExtracaoOpcoes Opcoes => opcoes.Value;

    /// <returns>A planilha preenchida (.xlsx), pronta para baixar.</returns>
    public byte[] Processar(
        Stream planilha, string pastaBase, int linhaInicial,
        Action<int, ResultadoLinha>? aoProcessar = null,   // (total de pedidos, resultado)
        CancellationToken ct = default)
    {
        using var wb = new XLWorkbook(planilha);
        var ws = wb.Worksheet(1);
        var ultima = ws.LastRowUsed()?.RowNumber() ?? 0;

        var pedidos = new List<(int Linha, string Valor)>();
        for (var r = Math.Max(linhaInicial, 1); r <= ultima; r++)
        {
            var v = ws.Cell(r, ColPedido).GetString().Trim();
            if (v.Length > 0) pedidos.Add((r, v));
        }

        foreach (var (linha, valor) in pedidos)
        {
            ct.ThrowIfCancellationRequested();
            ResultadoLinha resultado;
            try
            {
                var (dados, avisos) = LerProposta(valor, pastaBase);
                Gravar(ws, linha, dados);
                resultado = new ResultadoLinha(linha, valor,
                    avisos.Count > 0 ? StatusLinha.Aviso : StatusLinha.Ok,
                    avisos.Count > 0 ? string.Join("; ", avisos) : "OK");
            }
            catch (Exception ex)
            {
                resultado = new ResultadoLinha(linha, valor, StatusLinha.Erro, ex.Message);
            }
            aoProcessar?.Invoke(pedidos.Count, resultado);
        }

        using var saida = new MemoryStream();
        wb.SaveAs(saida);
        return saida.ToArray();
    }

    private (DadosProposta Dados, List<string> Avisos) LerProposta(string valorK, string pastaBase)
    {
        if (!NumeroPedido.TryParse(valorK, out var pedido))
            throw new ErroProposta($"Pedido em formato inesperado: '{valorK}'");

        var pdf = LocalizadorPdf.Localizar(pastaBase, pedido.Af, opcoes.Value.SubPasta);
        var (dados, achou) = ExtratorProposta.Extrair(
            LeitorPdf.LerTexto(pdf), pedido.Item, opcoes.Value.Rotulos);

        var avisos = new List<string>();
        if (!achou) avisos.Add($"item {pedido.Item} não identificado no PDF (usado o texto todo)");
        var falta = ExtratorProposta.Faltando(dados).ToList();
        if (falta.Count > 0) avisos.Add("não encontrado: " + string.Join(", ", falta));
        return (dados, avisos);
    }

    // Só preenche o que foi encontrado — nunca apaga o que já estava na célula.
    private static void Gravar(IXLWorksheet ws, int linha, DadosProposta d)
    {
        string[] valores = [d.Modelo, d.Serial, d.PartNumber, d.PrecoUnitario, d.PrecoTotal, d.Prazo];
        for (var i = 0; i < valores.Length; i++)
            if (valores[i].Length > 0)
                ws.Cell(linha, ColModelo + i).Value = valores[i];
    }
}
