# 자동 업데이트 올리기: BUILD +1 -> 빌드 -> GitHub(boxblock32/wjmaxonline) 에 커밋·릴리스.
# 사람들은 게임을 켤 때 새 빌드를 받고, 그다음 실행부터 적용된다.
$ErrorActionPreference = 'Continue'
$repo = 'boxblock32/wjmaxonline'
$mod = $PSScriptRoot
$core = "$mod\src\Core.cs"
$t = [IO.File]::ReadAllText($core)
$b = [int][regex]::Match($t, 'const int BUILD = (\d+);').Groups[1].Value + 1
[IO.File]::WriteAllText($core, ($t -replace 'const int BUILD = \d+;', "const int BUILD = $b;"), (New-Object Text.UTF8Encoding $true))
& "$mod\build.ps1" -Install
if (-not $?) { 'build failed'; exit 1 }
$dir = Join-Path $env:TEMP 'wjmax_release'
New-Item -ItemType Directory -Force $dir | Out-Null
[IO.File]::WriteAllText("$dir\build.txt", "$b")
git -C $mod add -A
git -C $mod commit -q -m "build $b"
git -C $mod push -q origin main 2>&1 | Out-Host
gh release create "b$b" "$mod\WJMAXOnline.dll" "$dir\build.txt" --repo $repo --title "build $b" --notes "자동 업데이트 빌드 $b"
if ($LASTEXITCODE -ne 0) { 'release failed'; exit 1 }
"build $b 올림. 사람들은 게임을 켜면 받아요."
