param(
    [string]$Configuration = "Release",
    [string]$Output = "$(Join-Path $PSScriptRoot '..\dist\win-x64')"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\GrabadorReuniones\GrabadorReuniones.csproj"

# Asegurar dotnet en PATH si está en ~/.dotnet
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
        $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
    }
}

Write-Host "Publicando Grabador de Reuniones IA (win-x64, ejecutable único portable)..."
dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:DebugType=none `
    -p:EnableWindowsTargeting=true `
    -o $Output

Write-Host ""
Write-Host "Listo: $Output\GrabadorReuniones.exe"
Write-Host "Ejecutable listo para usar (no requiere instalación ni permisos de administrador)."
