param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'
$mod  = $PSScriptRoot
$data = Join-Path (Split-Path $mod -Parent) 'WJMAX_Data'
$man  = Join-Path $data 'Managed'
$bak  = Join-Path $mod 'backup'
$files = 'ScriptingAssemblies.json', 'RuntimeInitializeOnLoads.json'
if (-not (Test-Path (Join-Path $man 'Assembly-CSharp.dll'))) { throw "Put the OnlineMod folder inside the WJMAX game folder (next to WJMAX.exe)." }

if ($Uninstall) {
    foreach ($f in $files) { if (Test-Path "$bak\$f") { Copy-Item "$bak\$f" "$data\$f" -Force } }
    Remove-Item "$man\WJMAXOnline.dll" -ErrorAction SilentlyContinue
    Write-Host 'Online mod removed (original files restored).'
    return
}

New-Item -ItemType Directory -Force $bak | Out-Null
foreach ($f in $files) { if (-not (Test-Path "$bak\$f")) { Copy-Item "$data\$f" "$bak\$f" } }
Copy-Item "$mod\WJMAXOnline.dll" "$man\WJMAXOnline.dll" -Force

# Always rebuild from the original backups so running install twice is harmless.
$sa = Get-Content "$bak\ScriptingAssemblies.json" -Raw | ConvertFrom-Json
$sa.names = @($sa.names) + 'WJMAXOnline.dll'
$sa.types = @($sa.types) + 16
[IO.File]::WriteAllText("$data\ScriptingAssemblies.json", ($sa | ConvertTo-Json -Compress -Depth 5))

$ri = Get-Content "$bak\RuntimeInitializeOnLoads.json" -Raw | ConvertFrom-Json
$ri.root = @($ri.root) + [pscustomobject]@{ assemblyName='WJMAXOnline'; nameSpace='WJMAXOnline'; className='OnlineMod'; methodName='Init'; loadTypes=0; isUnityClass=$false }
[IO.File]::WriteAllText("$data\RuntimeInitializeOnLoads.json", ($ri | ConvertTo-Json -Compress -Depth 5))

Write-Host 'Online mod installed. Start WJMAX.exe and choose the Online menu (or press F7 in FreeStyle).'
