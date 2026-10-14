param([string]$Version = (Get-Content "$PSScriptRoot/../version.txt" -Raw).Trim())
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be x.y.z' }
$tools = Join-Path $root '.tools'
New-Item -ItemType Directory -Force $tools | Out-Null
$package = Join-Path $tools 'cecil.zip'
if (!(Test-Path $package)) { Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Mono.Cecil/0.11.6' -OutFile $package }
if ((Get-FileHash $package -Algorithm SHA256).Hash -ne 'D2A23832AAA948BA9A01ACC42B5726E34C5F995958F1B30D45C0E7C70B3A72D5') { throw 'Mono.Cecil package checksum mismatch' }
Expand-Archive $package (Join-Path $tools 'cecil') -Force
$out = Join-Path $root "dist/versions/$Version"
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item (Join-Path $tools 'cecil/lib/net40/Mono.Cecil.dll') $out -Force
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.txt') $out -Force
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$app = Get-Content (Join-Path $root 'src/Application.cs') -Raw
$app = $app.Replace('const string CurrentVersion="0.1.0";', "const string CurrentVersion=`"$Version`";").Replace('AssemblyVersion("0.1.0.0")', "AssemblyVersion(`"$Version.0`" )")
$generated = Join-Path $tools 'Application.cs'
Set-Content $generated $app -Encoding utf8
& $csc /nologo /target:winexe /optimize+ "/out:$out/Application.exe" "/reference:$out/Mono.Cecil.dll" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $generated (Join-Path $root 'src/Patcher.cs') (Join-Path $root 'src/Updater.cs') (Join-Path $root 'src/SelfTest.cs') (Join-Path $root 'src/SteamLaunch.cs')
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed' }
& $csc /nologo /target:winexe /optimize+ "/out:$root/dist/Launcher.exe" /reference:System.Windows.Forms.dll (Join-Path $root 'src/Bootstrap.cs')
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap compilation failed' }
Set-Content (Join-Path $root 'dist/current.txt') $Version -NoNewline -Encoding ascii
$release = Join-Path $root 'build'
New-Item -ItemType Directory -Force $release | Out-Null
Compress-Archive -Path "$out/Application.exe","$out/Mono.Cecil.dll","$out/THIRD-PARTY-NOTICES.txt" -DestinationPath "$release/update.zip" -Force
$hash = (Get-FileHash "$release/update.zip" -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$release/update.sha256" $hash -Encoding ascii
$portable = Join-Path $release "portable-$Version"
New-Item -ItemType Directory -Force "$portable/versions/$Version" | Out-Null
Copy-Item "$root/dist/Launcher.exe","$root/dist/current.txt" $portable -Force
Copy-Item "$out/Application.exe","$out/Mono.Cecil.dll","$out/THIRD-PARTY-NOTICES.txt" "$portable/versions/$Version" -Force
Compress-Archive -Path "$portable/*" -DestinationPath "$release/CrossfireLegion-Launcher-$Version.zip" -Force
Write-Output "Build complete: $root/dist/Launcher.exe"
