# Generates detailed version iteration notes from git history.
#
# The script itself is PURE ASCII on purpose: PowerShell 5.1 mis-parses
# BOM-less UTF-8 CJK text, while GitHub Actions uses pwsh (UTF-8 default).
# All Chinese prose lives in release-notes-template.md and is read
# explicitly as UTF-8 via .NET, so every runtime behaves identically.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$PreviousTag = "",
    [string]$ArtifactsDir = "artifacts",
    [string]$Output = "artifacts/RELEASE_NOTES.md",
    [string]$Repository = "shenge-work/TempGuard",
    [string]$RunId = "",
    [string]$Template = ""
)

$ErrorActionPreference = "Stop"
$hasGit = [bool](Get-Command git -ErrorAction SilentlyContinue)

function Get-Commits([string]$range) {
    if (-not $hasGit) { return @() }
    if ([string]::IsNullOrWhiteSpace($range)) { return @() }
    $raw = & git log $range --no-merges --pretty=format:"%h%x1f%s%x1f%an%x1f%ad" --date=short 2>$null
    if (-not $raw) { return @() }
    $result = @()
    $separator = [string][char]0x1F
    foreach ($line in @($raw)) {
        $parts = $line -split $separator
        if ($parts.Count -ge 4) {
            $result += [pscustomobject]@{
                Hash    = $parts[0]
                Subject = $parts[1]
                Author  = $parts[2]
                Date    = $parts[3]
            }
        }
    }
    return $result
}

# ---- Comparison range ------------------------------------------------------
if ($hasGit -and [string]::IsNullOrWhiteSpace($PreviousTag)) {
    $detected = (& git describe --tags --abbrev=0 HEAD^ 2>$null)
    if ($detected) { $PreviousTag = [string]$detected }
}
$range = if ($PreviousTag) { $PreviousTag + '..HEAD' } else { 'HEAD' }
$commits = @(Get-Commits $range)

# ---- Group by conventional commit type ------------------------------------
$groups = @(@(), @(), @(), @(), @(), @())
foreach ($c in $commits) {
    $s = $c.Subject
    if ($s -match '^(?i)(feat|feature)(\(.+\))?\s*[:]') { $groups[0] += $c }
    elseif ($s -match '^(?i)(fix|bugfix|hotfix)(\(.+\))?\s*[:]') { $groups[1] += $c }
    elseif ($s -match '^(?i)(perf|refactor)(\(.+\))?\s*[:]') { $groups[2] += $c }
    elseif ($s -match '^(?i)(docs|doc)(\(.+\))?\s*[:]') { $groups[3] += $c }
    elseif ($s -match '^(?i)(build|ci|chore|test|style)(\(.+\))?\s*[:]') { $groups[4] += $c }
    else { $groups[5] += $c }
}

# ---- Artifact checksums ----------------------------------------------------
$checksums = @()
if (Test-Path $ArtifactsDir) {
    Get-ChildItem -Path $ArtifactsDir -File -Filter 'TempGuard-Setup-*.exe' | ForEach-Object {
        $checksums += [pscustomobject]@{
            Name   = $_.Name
            Size   = [math]::Round($_.Length / 1MB, 2)
            SHA256 = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
        }
    }
}

$date = [DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm') + ' UTC'
$head = if ($hasGit) { [string](& git rev-parse --short HEAD 2>$null) } else { 'local-build' }
if ([string]::IsNullOrWhiteSpace($head)) { $head = 'local-build' }
$baseline = if ($PreviousTag) { '`' + $PreviousTag + '`' } else { 'First release (full history)' }

# ---- Load template and fill placeholders ----------------------------------
if ([string]::IsNullOrWhiteSpace($Template)) {
    $Template = Join-Path $PSScriptRoot 'release-notes-template.md'
}
$templatePath = $Template
if (-not [System.IO.Path]::IsPathRooted($templatePath)) {
    $relative = Join-Path (Get-Location) $templatePath
    if (Test-Path $relative) { $templatePath = $relative }
    else { $templatePath = Join-Path $PSScriptRoot 'release-notes-template.md' }
}
if (-not (Test-Path $templatePath)) { throw "Template missing: $templatePath" }
$text = [System.IO.File]::ReadAllText($templatePath, [System.Text.Encoding]::UTF8)

$replacements = [ordered]@{
    '@@VERSION@@'      = $Version
    '@@DATE@@'         = $date
    '@@HEAD@@'         = [string]$head
    '@@BASELINE@@'     = $baseline
    '@@COMMIT_COUNT@@' = [string]$commits.Count
    '@@REPO@@'         = $Repository
    '@@RUN_ID@@'       = $(if ($RunId) { $RunId } else { 'N/A (local build)' })
}
foreach ($key in $replacements.Keys) {
    $text = $text.Replace($key, [string]$replacements[$key])
}

for ($i = 0; $i -lt 6; $i++) {
    $items = @($groups[$i])
    $text = $text.Replace('@@N' + ($i + 1) + '@@', [string]$items.Count)
    if ($items.Count -eq 0) {
        $body = '- None'
    } else {
        $rows = @()
        foreach ($c in $items) {
            $rows += '- `' + $c.Hash + '` ' + $c.Subject + ' - ' + $c.Author + ' / ' + $c.Date
        }
        $body = $rows -join "`n"
    }
    $text = $text.Replace('@@G' + ($i + 1) + '@@', $body)
}

if ($checksums.Count -eq 0) {
    $artifactRows = '| (installer not generated) | - | - |'
} else {
    $artifactRows = @()
    foreach ($a in $checksums) {
        $artifactRows += '| `' + $a.Name + '` | ' + $a.Size + ' | `' + $a.SHA256 + '` |'
    }
    $artifactRows = $artifactRows -join "`n"
}
$text = $text.Replace('@@ARTIFACTS@@', $artifactRows)

$outputPath = $Output
if (-not [System.IO.Path]::IsPathRooted($outputPath)) {
    $outputPath = Join-Path (Get-Location) $outputPath
}
$dir = Split-Path $outputPath -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[System.IO.File]::WriteAllText(
    $outputPath,
    $text,
    (New-Object System.Text.UTF8Encoding($false))
)
Write-Host ('RELEASE_NOTES_OK: ' + $Output + ' (' + $commits.Count + ' commits)')
