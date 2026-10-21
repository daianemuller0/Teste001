namespace HowdenExtrator.Poc.Models;

public enum StatusLinha { Ok, Aviso, Erro }

/// <summary>Resultado do processamento de uma linha da planilha.</summary>
public sealed record ResultadoLinha(int Linha, string Pedido, StatusLinha Status, string Detalhe);

/// <summary>Os seis campos lidos da proposta (colunas L a Q).</summary>
public sealed record DadosProposta(
    string Modelo, string Serial, string PartNumber,
    string PrecoUnitario, string PrecoTotal, string Prazo);
