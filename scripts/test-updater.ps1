param([string]$Version=(Get-Content "$PSScriptRoot/../version.txt" -Raw).Trim())
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root "dist/versions/$Version"
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /nowarn:0436 /target:exe "/out:$out/UpdaterTests.exe" "/reference:$out/Application.exe" /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $root 'src/Updater.cs') (Join-Path $root 'tests/UpdaterTests.cs')
if($LASTEXITCODE -ne 0){throw 'Updater test compile failed'}
& "$out/UpdaterTests.exe"
if($LASTEXITCODE -ne 0){throw 'Updater tests failed'}
