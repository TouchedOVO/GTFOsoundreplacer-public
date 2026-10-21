param(
    [string]$Configuration = 'Release',
    [string]$GTFO = $env:GTFO_GAME_PATH,
    [string]$Net6Framework = $env:NET6_FRAMEWORK_PATH,
    [string]$MSBuild = ''
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'GTFOSoundReplacer/GTFOSoundReplacer.csproj'
if ([string]::IsNullOrWhiteSpace($GTFO) -or !(Test-Path -LiteralPath (Join-Path $GTFO 'BepInEx'))) {
    throw 'Provide -GTFO with the game directory, or set GTFO_GAME_PATH.'
}
if ([string]::IsNullOrWhiteSpace($MSBuild)) {
    $found = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($found) { $MSBuild = $found.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        if (Test-Path -LiteralPath $vswhere) {
            $MSBuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        }
    }
}
if ([string]::IsNullOrWhiteSpace($MSBuild) -or !(Test-Path -LiteralPath $MSBuild)) {
    throw 'MSBuild was not found. Provide -MSBuild with its executable path.'
}
if ([string]::IsNullOrWhiteSpace($Net6Framework)) {
    $dotnetRoot = if ($env:DOTNET_ROOT) { $env:DOTNET_ROOT } else { Join-Path $env:ProgramFiles 'dotnet' }
    $runtimeRoot = Join-Path $dotnetRoot 'shared/Microsoft.NETCore.App'
    $runtime = Get-ChildItem -LiteralPath $runtimeRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^6\.0\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if ($runtime) { $Net6Framework = $runtime.FullName }
}
if ([string]::IsNullOrWhiteSpace($Net6Framework) -or !(Test-Path -LiteralPath (Join-Path $Net6Framework 'System.Private.CoreLib.dll'))) {
    throw 'A .NET 6 runtime directory is required. Provide -Net6Framework or set NET6_FRAMEWORK_PATH.'
}
& $MSBuild $project /nologo /verbosity:minimal "/p:Configuration=$Configuration" "/p:GTFO=$GTFO" "/p:BepInEx=$(Join-Path $GTFO 'BepInEx')" "/p:Net6Framework=$Net6Framework"
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
$out = Join-Path $PSScriptRoot "GTFOSoundReplacer/bin/$Configuration/GTFOSoundReplacer.dll"
& (Join-Path $PSScriptRoot 'GTFOSoundReplacer/tests/UnityGuiCompatibility.ps1') -Plugin $out -BepInExRoot (Join-Path $GTFO 'BepInEx')
Write-Host "Output: $out"
