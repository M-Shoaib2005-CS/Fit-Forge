# FitForge build & package script
$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$zipPath = "C:\Users\LENOVO\Desktop\FitForge_v5_final.zip"

Write-Host "Building FitForge..."
Push-Location $projectRoot
dotnet restore
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }

Write-Host "Creating zip (excluding bin, obj, .vs)..."
$tempDir = Join-Path $env:TEMP "FitForge_v5_pack"
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $tempDir | Out-Null

Get-ChildItem $projectRoot -Force | Where-Object {
    $_.Name -notin @('bin', 'obj', '.vs', 'build-and-zip.ps1')
} | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $tempDir $_.Name) -Recurse -Force
}

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -Force
Remove-Item $tempDir -Recurse -Force

$size = (Get-Item $zipPath).Length / 1MB
Write-Host "Done! Zip created: $zipPath ($([math]::Round($size, 2)) MB)"
Pop-Location
