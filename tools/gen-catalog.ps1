# GTFOSoundReplacer - sound catalog generator
# Reads wwiser-exported txtp files and extracts event name, event ID and
# objpath) per docs/CATALOG_DESIGN.md sec.1, classifies by objpath prefix per sec.3.2,
# and emits GTFOSoundReplacer\UI\SoundCatalogData.generated.cs for compile-time embedding.
#
# Usage:
#   pwsh -NoProfile -File tools/gen-catalog.ps1 -TxtpDir <txtp-directory>
#
# Dependency: .NET built-ins only (no System.Linq, no third-party modules).
param(
    [Parameter(Mandatory=$true)][string]$TxtpDir,
    [string]$OutFile = (Join-Path $PSScriptRoot '../GTFOSoundReplacer/UI/SoundCatalogData.generated.cs'),
    [string]$ReferenceJson = ''
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $TxtpDir)) { throw "txtp directory not found: $TxtpDir" }
$files = Get-ChildItem -Path $TxtpDir -Filter *.txtp

# ---- Parse regexes (docs/CATALOG_DESIGN.md sec.1; main event = 3-space-indented CAkEvent) ----
$eventIdRe = '^#\s{3}CAkEvent\[\d+\]\s+(\d+)'
$hashnameRe = '^#\s{3}-\s+hashname:\s*(.+)$'
$objpathRe = '^#\s{3}-\s+objpath:\s*(.+)$'

# ---- Classification rules (ordered, first match wins; sec.3.2) ----
$rules = @(
    @{ Prefix = '\Simons\sfx\enemies\';               Cat = 1 }   # Enemy
    @{ Prefix = '\Simons\sfx\enemies_henriks\';       Cat = 1 }
    @{ Prefix = '\Henkes\Weapons\';                   Cat = 0 }   # Weapon
    @{ Prefix = '\Simons\sfx\weapons\';               Cat = 0 }
    @{ Prefix = '\Simons\sfx\melee\';                 Cat = 0 }
    @{ Prefix = '\Simons\sfx\glue_gun\';              Cat = 0 }
    @{ Prefix = '\Simons\sfx\sentrygun\';             Cat = 0 }
    @{ Prefix = '\Simons\sfx\StickyMine\';            Cat = 0 }
    @{ Prefix = '\Simons\sfx\glue_grenade_and_mine\'; Cat = 0 }
    @{ Prefix = '\Henkes\BulletHit\';                 Cat = 0 }
    @{ Prefix = '\Simons\sfx\doors\';                 Cat = 2 }   # Door
    @{ Prefix = '\Simons\sfx\elevator\';              Cat = 2 }
    @{ Prefix = '\Henkes\Elevator\';                  Cat = 2 }
    @{ Prefix = '\Simons\sfx\reactor\';               Cat = 2 }
    @{ Prefix = '\Simons\sfx\battery_station\';       Cat = 2 }
    @{ Prefix = '\Simons\sfx\decontamination_unit\';  Cat = 2 }
    @{ Prefix = '\Simons\sfx\disinfection_machine\';  Cat = 2 }
    @{ Prefix = '\Simons\sfx\gui\';                   Cat = 3 }   # UI
    @{ Prefix = '\Simons\sfx\main_menu\';             Cat = 3 }
    @{ Prefix = '\Simons\sfx\game_menu\';             Cat = 3 }
    @{ Prefix = '\Simons\sfx\terminal\';              Cat = 3 }
    @{ Prefix = '\Simons\sfx\bioscan\';               Cat = 3 }
    @{ Prefix = '\Simons\sfx\hacking_puzzle\';        Cat = 3 }
    @{ Prefix = '\Simons\sfx\apex_puzzle\';           Cat = 3 }
    @{ Prefix = '\Simons\sfx\mapper\';                Cat = 3 }
    @{ Prefix = '\Simons\sfx\ambience\';              Cat = 4 }   # Ambience
    @{ Prefix = '\Simons\sfx\alarm\';                 Cat = 4 }
    @{ Prefix = '\Simons\sfx\emitters\';              Cat = 4 }
    @{ Prefix = '\Simons\sfx\infestation\';           Cat = 4 }
    @{ Prefix = '\Henkes\Ambiences\';                 Cat = 4 }
    @{ Prefix = '\Simons\sfx\Player_HENRIKS\';        Cat = 5 }   # Player
    @{ Prefix = '\Simons\sfx\player\';                Cat = 5 }
    @{ Prefix = '\Simons\sfx\foley\';                 Cat = 5 }
    @{ Prefix = '\Simons\breathing\';                 Cat = 5 }
    @{ Prefix = '\Simons\vo\';                        Cat = 6 }   # Voice
    @{ Prefix = '\Simons\sfx\text-to-speech_voice\';  Cat = 6 }
    @{ Prefix = '\Simons\music\';                     Cat = 8 }   # Music
    @{ Prefix = '\Henkes\Tools\';                     Cat = 7 }   # Item
    @{ Prefix = '\Henkes\Consumables\';               Cat = 7 }
    @{ Prefix = '\Henkes\Props\';                     Cat = 7 }
    @{ Prefix = '\Henkes\Pickups\';                   Cat = 7 }
    @{ Prefix = '\Henkes\Casings\';                   Cat = 7 }
    @{ Prefix = '\Simons\sfx\special_props\';         Cat = 7 }
    @{ Prefix = '\Simons\sfx\pickups\';               Cat = 7 }
    @{ Prefix = '\Simons\sfx\log_recs\';              Cat = 7 }
    @{ Prefix = '\Simons\sfx\biotracker\';            Cat = 7 }
    @{ Prefix = '\Simons\sfx\torch\';                 Cat = 7 }
    @{ Prefix = '\Simons\sfx\glow_stick\';            Cat = 7 }
    @{ Prefix = '\Simons\sfx\mapperdrone\';           Cat = 7 }
    @{ Prefix = '\Simons\sfx\fog_repeller\';          Cat = 7 }
    @{ Prefix = '\Simons\sfx\lockers_and_boxes\';     Cat = 7 }
)

function Get-Category([string]$objpath) {
    foreach ($r in $rules) {
        if ($objpath.StartsWith($r.Prefix, [System.StringComparison]::Ordinal)) {
            return [int]$r.Cat
        }
    }
    return 9   # Other
}

$catNames = @('Weapon','Enemy','Door','UI','Ambience','Player','Voice','Item','Music','Other')

# ---- Parse + dedup (EventId as key, first match wins) ----
$entries = @{}
$skipped = 0

foreach ($f in $files) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    $eventId = 0
    $name = $null
    $objpath = $null

    foreach ($line in $lines) {
        if ($eventId -eq 0) {
            $m = [regex]::Match($line, $eventIdRe)
            if ($m.Success) { $eventId = [uint32]$m.Groups[1].Value }
        }
        if ($null -eq $name) {
            $m = [regex]::Match($line, $hashnameRe)
            if ($m.Success) { $name = $m.Groups[1].Value.Trim() }
        }
        if ($null -eq $objpath) {
            $m = [regex]::Match($line, $objpathRe)
            if ($m.Success) { $objpath = $m.Groups[1].Value.Trim() }
        }
        if ($eventId -ne 0 -and $null -ne $name -and $null -ne $objpath) { break }
    }

    # No CAkEvent (pure Source dump) or incomplete fields -> skip
    if ($eventId -eq 0 -or [string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($objpath)) {
        $skipped++
        continue
    }

    if (-not $entries.ContainsKey($eventId)) {
        $entries[$eventId] = @{
            EventId  = $eventId
            Name     = $name
            Category = (Get-Category $objpath)
        }
    }
}

# Stable sort by EventId.
# Sort by the hashtable KEYS (the EventIds); the values are Hashtables and
# Sort-Object -Property does not resolve their keys as sort keys.
$sortedKeys = @($entries.Keys | Sort-Object)
$sorted = @()
foreach ($k in $sortedKeys) { $sorted += $entries[$k] }

# ---- Emit C# ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('// <auto-generated />')
[void]$sb.AppendLine('// GTFOSoundReplacer sound catalog (generated by tools/gen-catalog.ps1 from txtp exports).')
[void]$sb.AppendLine('// Unique events: ' + $sorted.Count + ' (deduped by EventId, name<->ID is 1:1).')
[void]$sb.AppendLine('// Regenerate: pwsh -NoProfile -File tools/gen-catalog.ps1 -TxtpDir <txtp-directory>')
[void]$sb.AppendLine('namespace GTFOSoundReplacer.UI')
[void]$sb.AppendLine('{')
[void]$sb.AppendLine('    /// <summary>Embedded .txtp sound catalog data (Name=event name, EventId=Wwise short ID, Category=objpath classification).</summary>')
[void]$sb.AppendLine('    public static class SoundCatalogData')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        /// <summary>Lightweight read-only entry (avoids allocating many SoundEntry instances).</summary>')
[void]$sb.AppendLine('        public readonly struct Entry')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            public readonly uint EventId;')
[void]$sb.AppendLine('            public readonly string Name;')
[void]$sb.AppendLine('            public readonly SoundCategory Category;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('            public Entry(uint eventId, string name, SoundCategory category)')
[void]$sb.AppendLine('            {')
[void]$sb.AppendLine('                EventId = eventId;')
[void]$sb.AppendLine('                Name = name;')
[void]$sb.AppendLine('                Category = category;')
[void]$sb.AppendLine('            }')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('        public static readonly Entry[] Entries = new Entry[]')
[void]$sb.AppendLine('        {')
foreach ($e in $sorted) {
    $escaped = $e.Name.Replace('\', '\\').Replace('"', '\"')
    [void]$sb.AppendLine('            new Entry(' + $e.EventId + 'u, "' + $escaped + '", SoundCategory.' + $catNames[$e.Category] + '),')
}
[void]$sb.AppendLine('        };')
[void]$sb.AppendLine('    }')
[void]$sb.AppendLine('}')

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), $utf8NoBom)

# ---- Summary stats ----
Write-Host ("txtp files: " + $files.Count)
Write-Host ("skipped (no CAkEvent / incomplete): " + $skipped)
Write-Host ("unique events: " + $sorted.Count)

$byCat = @{}
foreach ($e in $sorted) { $byCat[$e.Category] = ($byCat[$e.Category] + 1) }
for ($i = 0; $i -lt $catNames.Count; $i++) {
    $c = 0
    if ($byCat.ContainsKey($i)) { $c = $byCat[$i] }
    Write-Host ("  [" + $i + "] " + $catNames[$i] + " = " + $c)
}

Write-Host ("output: " + $OutFile)

# Compare with optional reference data.
if (-not [string]::IsNullOrWhiteSpace($ReferenceJson) -and (Test-Path -LiteralPath $ReferenceJson)) {
    Write-Host ("cross-check: " + $ReferenceJson)
    $ref = Get-Content -Path $ReferenceJson -Raw | ConvertFrom-Json
    Write-Host ("  reference JSON entries: " + $ref.Count)
    if ($ref.Count -ne $sorted.Count) {
        Write-Warning ("  count mismatch: generated " + $sorted.Count + " vs reference " + $ref.Count)
    }
    $refMap = @{}
    foreach ($r in $ref) { $refMap[[uint32]$r.EventId] = [int]$r.Category }
    $mismatch = 0
    foreach ($e in $sorted) {
        if ($refMap.ContainsKey($e.EventId)) {
            if ($refMap[$e.EventId] -ne $e.Category) {
                if ($mismatch -lt 20) {
                    Write-Warning ("  category mismatch: ID " + $e.EventId + " (" + $e.Name + ") generated=" + $e.Category + " reference=" + $refMap[$e.EventId])
                }
                $mismatch++
            }
        }
        else {
            if ($mismatch -lt 20) {
                Write-Warning ("  ID not in reference: " + $e.EventId + " (" + $e.Name + ")")
            }
            $mismatch++
        }
    }
    if ($mismatch -eq 0) {
        Write-Host ("  check passed: all EventId->Category match the reference.")
    }
    else {
        Write-Warning ("  mismatch count: " + $mismatch)
    }
}
