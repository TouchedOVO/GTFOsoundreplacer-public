param(
    [Parameter(Mandatory = $true)][string]$Plugin,
    [Parameter(Mandatory = $true)][string]$BepInExRoot
)
$ErrorActionPreference = 'Stop'
# Inspect metadata only; do not load Unity or execute game code in the test process.
Add-Type -Path (Join-Path $BepInExRoot 'core/Mono.Cecil.dll')
$taskResolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$taskResolver.AddSearchDirectory((Join-Path $BepInExRoot 'interop'))
$taskResolver.AddSearchDirectory((Join-Path $BepInExRoot 'core'))
$taskResolver.AddSearchDirectory((Split-Path ([object].Assembly.Location) -Parent))
$taskReader = [Mono.Cecil.ReaderParameters]::new()
$taskReader.AssemblyResolver = $taskResolver
$taskAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $Plugin).Path, $taskReader)
$script:taskUnityVisited = [Collections.Generic.HashSet[string]]::new()
$script:taskUnityFailures = [Collections.Generic.List[string]]::new()

function Test-UnityMethod {
    param([Mono.Cecil.MethodReference]$Reference, [string]$Route)
    if (!$script:taskUnityVisited.Add($Reference.FullName)) { return }
    $definition = $Reference.Resolve()
    if ($null -eq $definition) { throw "Cannot resolve Unity call: $($Reference.FullName)" }
    if (!$definition.HasBody) { return }
    $path = $Route + ' -> ' + $definition.FullName
    foreach ($instruction in $definition.Body.Instructions) {
        if ($instruction.OpCode.Name -eq 'ldstr' -and $instruction.Operand -eq 'Method unstripping failed') {
            $script:taskUnityFailures.Add($path)
            return
        }
    }
    foreach ($instruction in $definition.Body.Instructions) {
        if ($instruction.Operand -is [Mono.Cecil.MethodReference]) {
            $called = [Mono.Cecil.MethodReference]$instruction.Operand
            if ($called.DeclaringType.Namespace.StartsWith('UnityEngine', [StringComparison]::Ordinal)) {
                Test-UnityMethod -Reference $called -Route $path
            }
        }
    }
}

try {
    foreach ($type in $taskAssembly.MainModule.Types) {
        if (!$type.Namespace.StartsWith('GTFOSoundReplacer.UI', [StringComparison]::Ordinal) -and
            $type.FullName -ne 'GTFOSoundReplacer.HotkeyListener') { continue }
        foreach ($method in $type.Methods) {
            # Plain managed plugin types, including its enums, must not enter an injected Unity signature.
            $injectedComponent = $type.BaseType -and $type.BaseType.FullName -eq 'UnityEngine.MonoBehaviour'
            $hidden = @($method.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'Il2CppInterop.Runtime.Attributes.HideFromIl2CppAttribute' }).Count -gt 0
            if ($injectedComponent -and !$method.IsStatic -and !$method.IsConstructor -and !$hidden) {
                $signatureTypes = @($method.ReturnType) + @($method.Parameters | ForEach-Object { $_.ParameterType })
                foreach ($signatureType in $signatureTypes) {
                    if ($signatureType.Namespace.StartsWith('GTFOSoundReplacer', [StringComparison]::Ordinal)) {
                        $script:taskUnityFailures.Add("Managed-only type exposed to IL2CPP: $($method.FullName)")
                        break
                    }
                }
            }
            if (!$method.HasBody) { continue }
            foreach ($instruction in $method.Body.Instructions) {
                if ($instruction.Operand -is [Mono.Cecil.MethodReference]) {
                    $called = [Mono.Cecil.MethodReference]$instruction.Operand
                    if ($called.DeclaringType.Namespace.StartsWith('UnityEngine', [StringComparison]::Ordinal)) {
                        Test-UnityMethod -Reference $called -Route $method.FullName
                    }
                }
            }
        }
    }
    if ($script:taskUnityFailures.Count -gt 0) {
        throw ('Unity GUI compatibility audit failed:' + [Environment]::NewLine + ($script:taskUnityFailures -join [Environment]::NewLine))
    }
    Write-Output "Unity GUI compatibility audit passed: $($script:taskUnityVisited.Count) referenced Unity methods checked against installed interop assemblies."
} finally {
    $taskAssembly.Dispose()
    $taskResolver.Dispose()
}
