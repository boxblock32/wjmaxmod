# 자동 업데이트 올리기: BUILD +1 -> 빌드 -> GitHub(boxblock32/wjmaxonline) 에 커밋·릴리스.
# 사람들은 게임을 켤 때 새 빌드를 받고, 그다음 실행부터 적용된다.
$ErrorActionPreference = 'Continue'
$repo = 'boxblock32/wjmaxonline'
$mod = $PSScriptRoot
$key = "$mod\signkey.xml"
# 서명 키: 이 PC 에만 있음. 절대 GitHub·배포 zip 에 넣지 말 것. 잃어버리면 새 키로 배포 zip 을 다시 나눠줘야 함
if (-not (Test-Path $key)) { 'signkey.xml 이 없어요. 업데이트를 올릴 수 없어요.'; exit 1 }
$core = "$mod\src\Core.cs"
$t = [IO.File]::ReadAllText($core)
$b = [int][regex]::Match($t, 'const int BUILD = (\d+);').Groups[1].Value + 1
[IO.File]::WriteAllText($core, ($t -replace 'const int BUILD = \d+;', "const int BUILD = $b;"), (New-Object Text.UTF8Encoding $true))
& "$mod\build.ps1" -Install
if (-not $?) { 'build failed'; exit 1 }
$dir = Join-Path $env:TEMP 'wjmax_release'
New-Item -ItemType Directory -Force $dir | Out-Null
# build.txt = 빌드 번호 + "WJMAXOnline|빌드|DLL SHA256" 의 RSA 서명 (모드가 받기 전에 확인)
$sha = [Security.Cryptography.SHA256]::Create()
$msg = [Text.Encoding]::ASCII.GetBytes("WJMAXOnline|$b|" + [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes("$mod\WJMAXOnline.dll"))).Replace('-', ''))
$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.FromXmlString([IO.File]::ReadAllText($key))
$sig = $rsa.SignHash($sha.ComputeHash($msg), [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
[IO.File]::WriteAllText("$dir\build.txt", "$b`n" + [Convert]::ToBase64String($sig))
git -C $mod add -A
git -C $mod commit -q -m "build $b"
# GitHub 웹에서 고친 것(README 등)이 있으면 먼저 합침
git -C $mod pull -q --rebase origin main 2>&1 | Out-Host
git -C $mod push -q origin main 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) { 'push failed (릴리스 안 올림)'; exit 1 }
gh release create "b$b" "$mod\WJMAXOnline.dll" "$dir\build.txt" --repo $repo --target main --title "build $b" --notes "자동 업데이트 빌드 $b"
if ($LASTEXITCODE -ne 0) { 'release failed'; exit 1 }
"build $b 올림. 사람들은 게임을 켜면 받아요."
