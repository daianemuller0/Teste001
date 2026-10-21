namespace HowdenExtrator.Poc.Data;

/// <summary>Seção "Extracao" do appsettings.json.</summary>
public sealed class ExtracaoOpcoes
{
    public string PastaBase { get; set; } = @"H:\PV_AFTERMARKET";
    public string SubPasta { get; set; } = @"COMERCIAL\Proposta Comercial Valida";
    public int LinhaInicial { get; set; } = 2;
    public Dictionary<string, string> Rotulos { get; set; } = new();
}
