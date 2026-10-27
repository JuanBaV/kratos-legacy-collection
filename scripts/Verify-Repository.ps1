param([switch]$Staged)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $files = if ($Staged) { @(git diff --cached --name-only --diff-filter=ACMR) } else { @(git ls-files) }
    foreach ($file in $files) {
        if ($file -match '(?i)\.(iso|cso|chd|rom|elf|bin|exe|dll|pkg|pup|mcd|ps2|ppst|zip|7z|mp4|mkv|mp3|pfx|pem|key)$' -or $file -match '(^|/)(Data|Private|artifacts|\.build-home|\.vs)/') { throw "Private or distributable binary content is staged: $file" }
        if ($file -notmatch '\.(png|jpg|ico)$') {
            $content = if ($Staged) { (git show ":$file") -join "`n" } else { Get-Content -LiteralPath $file -Raw }
            if ($content -match '(?i)[A-Z]:[\\/]Users[\\/][^\\/\s"'']+' -or $content -match '(ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9]{30,}|-{5}BEGIN .*PRIVATE KEY-{5})') { throw "Review a possible private path or secret in $file" }
        }
    }
    git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace verification failed.' }
    Write-Output "Repository check passed for $($files.Count) files. Review screenshot contents and licenses separately."
} finally { Pop-Location }
