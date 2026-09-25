$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('CaptureToolAotGuard-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$validator = Join-Path $PSScriptRoot 'assert-native-aot-log.ps1'
$origin = 'Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels.Error.MessageConverter'
$cases = @(
    @{ Name = 'clean'; Log = 'App -> output'; Exit = 0; Pass = $true },
    @{ Name = 'known'; Log = "ILC : Trim analysis warning IL2026: $origin.Read(Utf8JsonReader&,Type,JsonSerializerOptions): Using member...`nILC : AOT analysis warning IL3050: $origin.Write(Utf8JsonWriter,Object,JsonSerializerOptions): Using member..."; Exit = 0; Pass = $true },
    @{ Name = 'known-other-pair'; Log = "ILC : warning IL3050: $origin.Read(Utf8JsonReader&,Type,JsonSerializerOptions): Using member...`nILC : warning IL2026: $origin.Write(Utf8JsonWriter,Object,JsonSerializerOptions): Using member..."; Exit = 0; Pass = $true },
    @{ Name = 'another-method'; Log = "ILC : warning IL2026: App.Read(): Calls $origin.Read()."; Exit = 0; Pass = $false },
    @{ Name = 'similar-method'; Log = "ILC : warning IL3050: $origin.ReadOther(): Using member..."; Exit = 0; Pass = $false },
    @{ Name = 'another-diagnostic'; Log = "ILC : warning IL2070: $origin.Read(): Using member..."; Exit = 0; Pass = $false },
    @{ Name = 'csharp-warning'; Log = 'App.cs(1): warning CS8600: Null conversion'; Exit = 0; Pass = $false },
    @{ Name = 'nonzero-exit'; Log = 'App -> output'; Exit = 1; Pass = $false },
    @{ Name = 'error-in-log'; Log = 'ILC : error IL3050: Unsupported operation'; Exit = 0; Pass = $false },
    @{ Name = 'empty'; Log = ''; Exit = 0; Pass = $false },
    @{ Name = 'missing'; Log = $null; Exit = 0; Pass = $false }
)
try {
    foreach ($case in $cases) {
        $path = Join-Path $root ($case.Name + '.log')
        if ($null -ne $case.Log) { Set-Content -LiteralPath $path -Value $case.Log }
        $accepted = $true
        try { & $validator -LogPath $path -PublishExitCode $case.Exit 6>$null }
        catch { $accepted = $false }
        if ($accepted -ne $case.Pass) { throw "AOT guard regression failed: $($case.Name)" }
    }
    Write-Host "All $($cases.Count) Native AOT diagnostic guard cases passed."
}
finally {
    # Delete only this script's own, explicitly checked temporary directory.
    $resolved = [IO.Path]::GetFullPath($root)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'CaptureToolAotGuard-*') { throw 'Unsafe temporary cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
