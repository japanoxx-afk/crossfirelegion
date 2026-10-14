param([Parameter(Mandatory=$true)][string]$GamePath,[ValidateSet('apply','restore')][string]$Action='apply',[string]$Version=(Get-Content "$PSScriptRoot/../version.txt" -Raw).Trim())
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root "dist/versions/$Version"
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:exe "/out:$out/PatchCli.exe" "/reference:$out/Application.exe" (Join-Path $PSScriptRoot 'PatchCli.cs')
if($LASTEXITCODE -ne 0){throw 'Patch command compilation failed'}
& "$out/PatchCli.exe" $Action $GamePath
if($LASTEXITCODE -ne 0){throw 'Patch operation failed'}
