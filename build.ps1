# Builds WJMAXOnline.dll from src\*.cs against the game's own assemblies (needs the .NET SDK).
# -Dev includes the dev command tools (dev\cmd.txt). -Install copies the DLL into the game.
param([switch]$Dev, [switch]$Install)
$ErrorActionPreference = 'Stop'
$mod = $PSScriptRoot
$man = Join-Path (Split-Path $mod -Parent) 'WJMAX_Data\Managed'
$csc = Get-ChildItem "$env:ProgramFiles\dotnet\sdk\*\Roslyn\bincore\csc.dll" | Sort-Object FullName | Select-Object -Last 1
$refs = 'mscorlib','netstandard','System','System.Core','UnityEngine','UnityEngine.CoreModule','UnityEngine.IMGUIModule',
        'UnityEngine.TextRenderingModule','UnityEngine.InputLegacyModule','UnityEngine.AudioModule','UnityEngine.UI',
        'UnityEngine.UIModule','UnityEngine.ScreenCaptureModule','UnityEngine.VideoModule','UnityEngine.AnimationModule',
        'Unity.TextMeshPro','Assembly-CSharp' | ForEach-Object { "-r:$man\$_.dll" }
$define = if ($Dev) { '-define:DEV' } else { '-define:RELEASE' }
& dotnet $csc.FullName -nologo -noconfig -nostdlib -target:library -optimize -langversion:9 -nowarn:618 $define "-out:$mod\WJMAXOnline.dll" @refs (Get-ChildItem "$mod\src\*.cs").FullName
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
if ($Install) { Copy-Item "$mod\WJMAXOnline.dll" "$man\WJMAXOnline.dll" -Force }
'build ok' + $(if ($Dev) { ' (dev)' })
