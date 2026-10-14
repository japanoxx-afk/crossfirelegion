param([string]$GamePath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root '.tools/SteamLaunchTests.exe'
New-Item -ItemType Directory -Force (Split-Path $out -Parent) | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:exe "/out:$out" (Join-Path $root 'src/SteamLaunch.cs') (Join-Path $root 'tests/SteamLaunchTests.cs')
if($LASTEXITCODE -ne 0){throw 'Steam tests compilation failed'}
if($GamePath){& $out $GamePath}else{& $out}
if($LASTEXITCODE -ne 0){throw 'Steam launch regression tests failed'}
