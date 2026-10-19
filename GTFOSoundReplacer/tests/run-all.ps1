param(
    [ValidateSet('All','Route','Player','Core')][string]$Suite = 'All',
    [string]$PowerShellPath = (Get-Process -Id $PID).Path
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Tests require PowerShell 7 with System.Text.Json (pwsh).' }
if ($Suite -eq 'All') {
    foreach ($part in @('Route','Player','Core')) {
        & $PowerShellPath -NoProfile -File $PSCommandPath -Suite $part -PowerShellPath $PowerShellPath
        if ($LASTEXITCODE -ne 0) { throw "$part checks failed with exit code $LASTEXITCODE" }
    }
    return
}
$root = Split-Path $PSScriptRoot -Parent
$files = switch ($Suite) {
    'Route' { @('Audio/RouteBindingState.cs','tests/ListenerBindingChecks.cs') }
    'Player' { @('Audio/RouteBindingState.cs','Audio/ListenerRouting.cs','Audio/SoundBankPlayer.cs','tests/PlayerBoundaryChecks.cs') }
    'Core' { @(
        'Audio/ReplacementEntry.cs','Audio/ReplacementMap.cs',
        'Presets/PresetData.cs','Presets/PresetCodec.cs','Presets/PresetManager.cs',
        'UI/IPresetManager.cs','UI/SoundEntry.cs','UI/SoundCategory.cs','UI/ISoundCatalog.cs',
        'UI/SoundCatalog.cs','UI/SoundCatalogData.generated.cs',
        'tests/CoreTests.cs','tests/PresetManagerChecks.cs'
    ) }
}
$paths = @($files | ForEach-Object { Join-Path $root $_ })
if ($Suite -eq 'Core') {
    # Real JSON and collections assemblies from the current pwsh runtime; no NuGet or game install.
    $references = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)
    Add-Type -Path $paths -ReferencedAssemblies $references
    "Core production checks passed: $([CoreTests]::Run())"
    "Preset manager production checks passed: $([PresetManagerChecks]::Run())"
} else {
    Add-Type -Path $paths
    if ($Suite -eq 'Route') { "Listener binding production checks passed: $([ListenerBindingChecks]::Run())" }
    else { "Player boundary production checks passed: $([PlayerBoundaryChecks]::Run())" }
}
