param(
    [Parameter(Mandatory)][string]$LogPath,
    [Parameter(Mandatory)][int]$PublishExitCode
)
$ErrorActionPreference = 'Stop'
if ($PublishExitCode -ne 0) { throw "Native AOT publish failed ($PublishExitCode). See $LogPath" }
if (!(Test-Path -LiteralPath $LogPath -PathType Leaf)) { throw "Missing publish log: $LogPath" }
$log = Get-Content -LiteralPath $LogPath -Raw
if ([string]::IsNullOrWhiteSpace($log)) { throw "Empty publish log: $LogPath" }
if ($log -match '(?im)\berror\s+[A-Z]+\d+\s*:|\bfatal error\b') { throw "Publish log contains an error. See $LogPath" }

# Exception: Foundry Local 1.2.4 / Betalgo 9.1.0. Match the diagnostic AND its
# originating method, not a mention of that method later in another warning.
# Revisit on dependency upgrades; a clean publish needs no exception.
$known = 0
foreach ($diagnostic in [regex]::Matches($log, '(?im)\bwarning\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>[^\r\n]*)')) {
    $code = $diagnostic.Groups['code'].Value
    $message = $diagnostic.Groups['message'].Value
    if ($code -notin 'IL2026', 'IL3050' -or
        $message -notmatch '^Betalgo\.Ranul\.OpenAI\.ObjectModels\.ResponseModels\.Error\.MessageConverter\.(Read|Write)\(') {
        throw "Unexpected Native AOT warning ${code}: $message. See $LogPath"
    }
    $known++
}
Write-Host "Native AOT publish verified: $known accepted vendor warnings ($LogPath)."
