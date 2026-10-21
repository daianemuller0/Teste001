# Guia Arquitetural — Extrator de Propostas

Mesma estrutura dos projetos **Serviços** e **Axiais**: Blazor Server (.NET 8) hospedado num
`BackendHost`, que roda como site (`dotnet run`) ou dentro de uma janela WinForms + WebView2
(`desktop/`), com lançador na rede (`launcher/`) e `publicar.ps1`.

## O que o programa faz

1. Você envia a planilha Excel. O pedido está na coluna **K** (`AF3242-01` → AF `AF3242`, item `1`).
2. Para cada linha, abre `PastaBase\<AF>\COMERCIAL\Proposta Comercial Valida` (deve haver **1** PDF).
3. Lê o texto do PDF (somente leitura) e acha o item da proposta.
4. Preenche **L** modelo · **M** serial · **N** part number · **O** preço unitário · **P** preço total · **Q** prazo.
5. Você baixa a cópia `<nome>_preenchido.xlsx`. A planilha enviada e os PDFs não são alterados.

Não há login nem banco de dados (diferente do Serviços): nada é guardado entre execuções.

## Estrutura

```
├── Program.cs / BackendHost.cs   # modo navegador; fábrica do servidor (porta 5083)
├── appsettings.json              # PastaBase, SubPasta, LinhaInicial, Rotulos
├── HowdenExtrator.Poc.csproj     # net8.0 + PdfPig + ClosedXML
├── Components/                   # App, Routes, Layout (MainLayout/NavMenu), Pages
│   └── Pages/Extrair.razor       # tela principal · Diagnostico.razor mostra o texto do PDF
├── Data/
│   ├── NumeroPedido.cs           # 'AF3242-01' → (AF, item)
│   ├── LocalizadorPdf.cs         # acha o único PDF da pasta do pedido
│   ├── LeitorPdf.cs              # PDF → texto por linha (somente leitura)
│   ├── ExtratorProposta.cs       # ★ recorta o item e extrai os 6 campos por rótulo
│   └── ProcessadorPlanilha.cs    # percorre a coluna K e grava L–Q
├── Models/ResultadoLinha.cs
├── wwwroot/ app.css (igual ao Serviços) · app.js
├── desktop/                      # WinForms + WebView2 + Kestrel no mesmo processo (HowdenExtrator.exe)
├── launcher/                     # EP.exe: copia a versão da rede e abre o app
└── publicar.ps1                  # publica versão nova na rede
```

## Ajustando a extração

Os rótulos procurados (Model, Serial Number, Part Number, Unit Price, Total Price, Lead Time e
equivalentes em português) estão em `ExtratorProposta.RotulosPadrao`. Para mudar sem recompilar,
sobrescreva em `appsettings.json` → `Extracao:Rotulos` (chaves `modelo`, `serial`, `partNumber`,
`precoUnitario`, `precoTotal`, `prazo`; valor = expressão regular do rótulo).
Use **Diagnóstico de PDF** para ver o texto exato que o programa lê.

## Rodar no VS Code

```
dotnet run            # abre http://localhost:5083 no navegador
```
Modo janela (Windows): `dotnet run --project desktop`.
