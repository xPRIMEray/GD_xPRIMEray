[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$MoviePath,
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][int]$FramesPerSecond,
    [Parameter(Mandatory = $true)][int]$Width,
    [Parameter(Mandatory = $true)][int]$Height
)

# CQ0.5-STAB B3: MovieWriter finalizes the AVI only at engine exit, so the fixture cannot verify the movie it
# records. This post-process check is mandatory for Demo 001: a structurally valid, finalized AVI whose header
# matches the manifest and the requested capture contract. Exits 0 and prints one line on success; throws otherwise.
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $MoviePath -PathType Leaf)) { throw "Movie was not produced: $MoviePath" }
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw "Manifest was not produced: $ManifestPath" }
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$expectedFrames = [int]$manifest.capture.total_frames
if ($expectedFrames -le 0) { throw "Manifest reports no frames: $ManifestPath" }
if ([int]$manifest.capture.fixed_fps -ne $FramesPerSecond) { throw "Manifest fps $($manifest.capture.fixed_fps) != requested $FramesPerSecond" }
if ([int]$manifest.capture.resolution.width -ne $Width -or [int]$manifest.capture.resolution.height -ne $Height) {
    throw "Manifest resolution $($manifest.capture.resolution.width)x$($manifest.capture.resolution.height) != requested ${Width}x${Height}"
}

$stream = [System.IO.File]::OpenRead($MoviePath)
try {
    $length = $stream.Length
    if ($length -lt 4096) { throw "Movie is empty or truncated ($length bytes): $MoviePath" }
    $reader = New-Object System.IO.BinaryReader($stream)
    $header = $reader.ReadBytes(4096)
    $ascii = [System.Text.Encoding]::ASCII
    if ($ascii.GetString($header, 0, 4) -ne 'RIFF' -or $ascii.GetString($header, 8, 4) -ne 'AVI ') { throw "Not a RIFF/AVI file: $MoviePath" }
    $avih = -1
    for ($i = 12; $i -le $header.Length - 64; $i++) {
        if ($header[$i] -eq 0x61 -and $ascii.GetString($header, $i, 4) -eq 'avih') { $avih = $i; break }
    }
    if ($avih -lt 0) { throw "AVI main header (avih) not found: $MoviePath" }
    $body = $avih + 8
    $usPerFrame = [BitConverter]::ToUInt32($header, $body)
    $totalFrames = [BitConverter]::ToUInt32($header, $body + 16)
    $aviWidth = [BitConverter]::ToUInt32($header, $body + 32)
    $aviHeight = [BitConverter]::ToUInt32($header, $body + 36)
    if ($usPerFrame -eq 0 -or [Math]::Round(1000000.0 / $usPerFrame) -ne $FramesPerSecond) { throw "AVI frame interval $usPerFrame us does not match $FramesPerSecond fps" }
    if ($aviWidth -ne $Width -or $aviHeight -ne $Height) { throw "AVI dimensions ${aviWidth}x${aviHeight} != ${Width}x${Height}" }
    if ($totalFrames -ne $expectedFrames) { throw "AVI total frames $totalFrames != manifest total_frames $expectedFrames (unfinalized or incomplete movie)" }
    # The writer appends idx1 (16 bytes per video or audio chunk) as the final chunk when it finalizes the file.
    $finalized = $false
    foreach ($entries in @(($totalFrames * 2), $totalFrames)) {
        $offset = $length - 8 - (16 * [long]$entries)
        if ($offset -le 12) { continue }
        $stream.Position = $offset
        $tag = $ascii.GetString($reader.ReadBytes(8), 0, 4)
        if ($tag -eq 'idx1') { $finalized = $true; break }
    }
    if (-not $finalized) { throw "AVI index (idx1) not found at end of file; movie was not finalized: $MoviePath" }
} finally {
    $stream.Dispose()
}
Write-Output ("DEMO001_MOVIE_VERIFIED frames={0} fps={1} resolution={2}x{3} bytes={4}" -f $totalFrames, $FramesPerSecond, $aviWidth, $aviHeight, $length)
