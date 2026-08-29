<#
.SYNOPSIS
    Dry-run verification pass for the ABUVI photo archive import.
    Reads folders only. Never touches the database or blob storage.

.DESCRIPTION
    Walks one or more root folders (one per contributor/location), finds every
    media file, and for each one tries to work out:
      - Contributor (first folder segment under the root you pass in)
      - Year        (first folder segment, walking from the file outward, that
                      starts with a plausible camp year)
      - Venue guess (whatever text follows the year in that same segment)
      - SHA-256 hash, to catch duplicates -- including duplicates that live
        under two different contributors' folders

    It does NOT write anything. It only produces a CSV report so a human can
    review the year/venue guesses and the duplicate groups before any upload
    script runs against the real data.

.PARAMETER RootPaths
    One or more root folders to scan. Pass each contributor's/location's
    top-level folder separately, e.g.:
      -RootPaths "D:\Historia Abuvi\Timo\Abuvi", "D:\Historia Abuvi\Maruja\Abuvi contenido"

.PARAMETER OutCsv
    Where to write the report. Defaults next to this script.

.EXAMPLE
    .\verify-media-import.ps1 -RootPaths "D:\Historia Abuvi\Timo\Abuvi", "D:\Historia Abuvi\Maruja\Abuvi contenido"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$RootPaths,

    [string]$OutCsv = (Join-Path $PSScriptRoot "media-import-review.csv"),

    [int]$ExpectedYearMin = 1976,
    [int]$ExpectedYearMax = 2026
)

$ErrorActionPreference = "Stop"

# Matches the four extension lists from BlobStorageOptions (appsettings.json).
$ImageExt = @(".jpg", ".jpeg", ".png", ".webp", ".gif")
$VideoExt = @(".mp4", ".mov", ".avi", ".webm")
$AudioExt = @(".mp3", ".wav", ".ogg", ".m4a", ".flac", ".aac")
$DocExt   = @(".pdf", ".doc", ".docx")
$AllExt   = $ImageExt + $VideoExt + $AudioExt + $DocExt

function Get-MediaType([string]$ext) {
    if ($ImageExt -contains $ext) { return "Photo" }
    if ($VideoExt -contains $ext) { return "Video" }
    if ($AudioExt -contains $ext) { return "Audio" }
    if ($DocExt   -contains $ext) { return "Document" }
    return "Unknown"
}

# 4-digit year at the START of a segment, optionally followed by a separator
# (space, dash, "- ") and then the venue text. Deliberately does NOT accept a
# bare 2-digit year here -- that rule needs venue-name evidence in the same
# segment to be safe (see EditionResolver plan notes), which a filename-only
# script can't verify reliably. Bare 2-digit segments are reported as
# "unresolved" rather than guessed at.
$YearPattern = '^(?<year>19[7-9]\d|20[0-4]\d)\s*-?\s*(?<venue>.*)$'

function Resolve-YearAndVenue([string]$relativePathUnderRoot) {
    $segments = $relativePathUnderRoot -split '[\\/]' | Where-Object { $_ -ne "" }
    # Walk from the file's own folder outward (deepest first) -- first match wins,
    # same precedence rule as the EditionResolver plan.
    for ($i = $segments.Count - 2; $i -ge 0; $i--) {
        $seg = $segments[$i].Trim()
        if ($seg -match $YearPattern) {
            $venue = $Matches['venue'].Trim()
            return [pscustomobject]@{
                Year        = [int]$Matches['year']
                VenueGuess  = $venue
                MatchedFrom = $seg
            }
        }
    }
    return [pscustomobject]@{ Year = $null; VenueGuess = $null; MatchedFrom = $null }
}

$rows = New-Object System.Collections.Generic.List[object]

foreach ($root in $RootPaths) {
    $resolvedRoot = (Resolve-Path -LiteralPath $root -ErrorAction SilentlyContinue)
    if (-not $resolvedRoot) {
        Write-Warning "Root not found, skipping: $root"
        continue
    }
    $rootFull = $resolvedRoot.Path.TrimEnd('\', '/')
    Write-Host "Scanning: $rootFull" -ForegroundColor Cyan

    $files = Get-ChildItem -LiteralPath $rootFull -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $AllExt -contains $_.Extension.ToLowerInvariant() }

    foreach ($f in $files) {
        $relativeToRoot = $f.FullName.Substring($rootFull.Length).TrimStart('\', '/')
        $segments = $relativeToRoot -split '[\\/]'
        $contributor = if ($segments.Count -gt 1) { $segments[0] } else { "(root)" }

        $match = Resolve-YearAndVenue $relativeToRoot
        $yearInRange = $null
        if ($null -ne $match.Year) {
            $yearInRange = ($match.Year -ge $ExpectedYearMin -and $match.Year -le $ExpectedYearMax)
        }

        $hash = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash

        $rows.Add([pscustomobject]@{
            Root           = $rootFull
            Contributor    = $contributor
            RelativePath   = $relativeToRoot
            MediaType      = Get-MediaType $f.Extension.ToLowerInvariant()
            Extension      = $f.Extension.ToLowerInvariant()
            SizeBytes      = $f.Length
            DetectedYear   = $match.Year
            YearInRange    = $yearInRange
            VenueGuess     = $match.VenueGuess
            MatchedSegment = $match.MatchedFrom
            Sha256         = $hash
            FullPath       = $f.FullName
        })
    }
}

if ($rows.Count -eq 0) {
    Write-Warning "No media files found under the given roots. Nothing to report."
    return
}

# --- Duplicate detection across ALL roots combined ---
$byHash = $rows | Group-Object Sha256
foreach ($group in $byHash) {
    $isDup = $group.Count -gt 1
    foreach ($row in $group.Group) {
        $row | Add-Member -NotePropertyName IsDuplicate -NotePropertyValue $isDup -Force
        $row | Add-Member -NotePropertyName DuplicateGroupSize -NotePropertyValue $group.Count -Force
    }
}

$rows | Sort-Object @{Expression = { $_.DetectedYear -eq $null }; Descending = $true }, IsDuplicate -Descending |
    Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8

# --- Summary ---
$total = $rows.Count
$unresolved = ($rows | Where-Object { $null -eq $_.DetectedYear }).Count
$outOfRange = ($rows | Where-Object { $_.YearInRange -eq $false }).Count
$dupGroups = ($byHash | Where-Object { $_.Count -gt 1 }).Count
$dupFiles = ($rows | Where-Object { $_.IsDuplicate }).Count
$contributors = ($rows | Select-Object -ExpandProperty Contributor -Unique)
$byType = $rows | Group-Object MediaType | Sort-Object Count -Descending

Write-Host ""
Write-Host "=== Verification summary ===" -ForegroundColor Green
Write-Host "Total files scanned:      $total"
Write-Host "Contributors detected:    $($contributors -join ', ')"
foreach ($t in $byType) { Write-Host "  $($t.Name): $($t.Count)" }
Write-Host "Unresolved (no year):     $unresolved"
Write-Host "Year outside $ExpectedYearMin-$ExpectedYearMax`:    $outOfRange"
Write-Host "Duplicate groups found:   $dupGroups (covering $dupFiles files)"
Write-Host ""
Write-Host "Full report written to: $OutCsv" -ForegroundColor Yellow
Write-Host "Open it and check, in this order: (1) unresolved rows, (2) out-of-range years, (3) duplicate groups."
