# AozoraEpub3 .NET - Publish Script (Windows)
# Usage: .\publish.ps1 [-Gui] [-Cli] [-SelfContained]

param(
    [switch]$Gui,
    [switch]$Cli,
    [switch]$SelfContained,
    [string]$Output = "dist"
)

# Default: build both
if (-not $Gui -and -not $Cli) {
    $Gui = $true
    $Cli = $true
}

$sc = if ($SelfContained) { "--self-contained true" } else { "--self-contained false" }
$rid = "win-x64"

function Publish-Project {
    param([string]$Project, [string]$OutDir)
    Write-Host "`n=== Publishing $Project ===" -ForegroundColor Cyan
    $cmd = "dotnet publish `"$Project`" -c Release -r $rid $sc -o `"$OutDir`""
    Write-Host $cmd
    Invoke-Expression $cmd
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed for $Project"
        exit 1
    }
}

if ($Cli) {
    Publish-Project "src\AozoraEpub3.Cli\AozoraEpub3.Cli.csproj" "$Output\cli"
    Write-Host "`nCLI published to: $Output\cli\AozoraEpub3.Cli.exe" -ForegroundColor Green
}

if ($Gui) {
    Publish-Project "src\AozoraEpub3.Gui\AozoraEpub3.Gui.csproj" "$Output\gui"
    Write-Host "`nGUI published to: $Output\gui\AozoraEpub3.Gui.exe" -ForegroundColor Green
}

Write-Host "`n=== Done! ===" -ForegroundColor Green
