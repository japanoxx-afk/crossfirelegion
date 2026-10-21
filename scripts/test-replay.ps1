param([string]$Version=(Get-Content "$PSScriptRoot/../version.txt" -Raw).Trim())
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root "dist/versions/$Version"
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:exe "/out:$out/ReplayTests.exe" "/reference:$out/Application.exe" /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $root 'tests/ReplayTests.cs')
if($LASTEXITCODE -ne 0){throw 'Replay tests compilation failed'}
& "$out/ReplayTests.exe"
if($LASTEXITCODE -ne 0){throw 'Replay tests failed'}
