param([string]$GamePath,[string]$Version='0.2.0')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root "dist/versions/$Version"
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:library /optimize+ "/out:$out/MovementFixture.dll" (Join-Path $root 'tests/MovementFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Movement fixture compilation failed'}
& $csc /nologo /target:exe "/out:$out/RuntimeTests.exe" "/reference:$out/Application.exe" "/reference:$out/Mono.Cecil.dll" (Join-Path $root 'src/Runtime.cs') (Join-Path $root 'src/Cheats.cs') (Join-Path $root 'src/Startup.cs') (Join-Path $root 'tests/RuntimeTests.cs') (Join-Path $root 'tests/CheatTests.cs') (Join-Path $root 'tests/StartupInstrumentationTests.cs') (Join-Path $root 'tests/StartupOptimizationTests.cs') (Join-Path $root 'tests/MovementTests.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime test compilation failed'}
if($GamePath){& "$out/RuntimeTests.exe" $GamePath}else{& "$out/RuntimeTests.exe"}
if($LASTEXITCODE -ne 0){throw 'Runtime regression tests failed'}
