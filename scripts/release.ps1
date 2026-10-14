param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be x.y.z' }
Set-Location (Split-Path $PSScriptRoot -Parent)
if (git status --porcelain) { throw 'Commit or stash changes before releasing' }
if ([version]$Version -le [version](Get-Content version.txt -Raw).Trim()) { throw 'New version must be higher than version.txt' }
Set-Content version.txt $Version -Encoding ascii
git add version.txt
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) { throw 'Commit failed' }
git tag "v$Version"
if ($LASTEXITCODE -ne 0) { throw 'Tag failed' }
git push origin main "v$Version"
if ($LASTEXITCODE -ne 0) { throw 'Push failed' }
