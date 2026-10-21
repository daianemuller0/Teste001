# =====================================================================
# Publica o EP na pasta de rede, em versao nova (ninguem trava arquivo):
#
#   \\BZVCPFIL003\proj_ramires$\EP\
#     EP.exe               <- lancador (atalho dos usuarios aponta pra ele)
#     app\versao.txt       <- nome da versao atual (ex.: v2026-09-09_1530)
#     app\v2026-09-09_1530\  <- arquivos publicados desta versao
#
# (a base de dados continua em \\BZVCPFIL003\proj_ramires$\DB\servicos,
#  a mesma do appsettings.json - o desktop usa a mesma base do site)
#
# Uso:  .\publicar.ps1            (publica no caminho padrao)
#       .\publicar.ps1 -Destino \\outro\caminho
# =====================================================================
param(
    [string]$Destino = '\\BZVCPFIL003\proj_ramires$\EP',
    [int]$ManterVersoes = 5
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$versao = 'v' + (Get-Date -Format 'yyyy-MM-dd_HHmm')

# Tudo o que aparecer na tela tambem fica gravado em publicar.log -
# se algo falhar, e so enviar esse arquivo.
try { Stop-Transcript | Out-Null } catch { }
Start-Transcript -Path "$raiz\publicar.log" -Force | Out-Null

if (-not (Test-Path "$raiz\desktop\HowdenExtrator.Desktop.csproj")) {
    Stop-Transcript | Out-Null
    throw "A pasta 'desktop' nao existe aqui - rode 'git pull' para baixar a versao mais nova do projeto e tente de novo."
}

Write-Host "== EP: publicando versao $versao em $Destino ==" -ForegroundColor Cyan

# 1) Compila o app desktop (auto-contido: as maquinas nao precisam ter .NET)
Write-Host "`n[1/5] Compilando o aplicativo..." -ForegroundColor Yellow
dotnet publish "$raiz\desktop\HowdenExtrator.Desktop.csproj" -c Release -r win-x64 `
    --self-contained true -p:PublishReadyToRun=true -o "$raiz\out\app\$versao"
if ($LASTEXITCODE -ne 0) {
    Stop-Transcript | Out-Null
    throw 'Falha ao compilar o aplicativo desktop - os detalhes estao acima e em publicar.log.'
}

# 2) Compila o lancador (um .exe unico e pequeno)
Write-Host "`n[2/5] Compilando o lancador..." -ForegroundColor Yellow
dotnet publish "$raiz\launcher\HowdenExtrator.Launcher.csproj" -c Release -r win-x64 `
    --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true `
    -p:EnableCompressionInSingleFile=false -o "$raiz\out\launcher"
if ($LASTEXITCODE -ne 0) {
    Stop-Transcript | Out-Null
    throw 'Falha ao compilar o lancador - os detalhes estao acima e em publicar.log.'
}

# 3) Estrutura da pasta de rede + copia da versao nova
Write-Host "`n[3/5] Copiando a versao para a rede..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "$Destino\app" | Out-Null
robocopy "$raiz\out\app\$versao" "$Destino\app\$versao" /E /R:2 /W:2 /NP /NFL /NDL | Out-Null
if ($LASTEXITCODE -ge 8) {
    Stop-Transcript | Out-Null
    throw "Falha ao copiar os arquivos para a rede (robocopy $LASTEXITCODE)."
}

# 4) Vira a chave: escreve a versao SO DEPOIS da copia terminar.
#    Quem abrir a partir de agora ja baixa a versao nova.
Write-Host "`n[4/5] Ativando a versao $versao..." -ForegroundColor Yellow
Set-Content -Path "$Destino\app\versao.txt" -Value $versao -Encoding ASCII

# Lancador: copia se mudou (se estiver em uso por alguem, avisa e segue)
try {
    Copy-Item "$raiz\out\launcher\EP.exe" "$Destino\EP.exe" -Force
} catch {
    Write-Warning "Nao consegui atualizar o EP.exe (alguem deve estar abrindo o app agora). Rode de novo mais tarde se o lancador tiver mudado."
}

# 5) Limpa versoes antigas na rede (mantem as ultimas N)
Write-Host "`n[5/5] Limpando versoes antigas..." -ForegroundColor Yellow
Get-ChildItem "$Destino\app" -Directory -Filter 'v*' |
    Sort-Object Name -Descending |
    Select-Object -Skip $ManterVersoes |
    ForEach-Object {
        Write-Host "  removendo $($_.Name)"
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }

Write-Host "`n== Pronto! Versao $versao publicada. ==" -ForegroundColor Green
Write-Host "Atalho para os usuarios: $Destino\EP.exe"
Write-Host 'Quem estiver com o programa aberto recebe a atualizacao na proxima vez que abrir.'
Stop-Transcript | Out-Null
