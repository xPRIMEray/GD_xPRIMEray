[CmdletBinding()]
param(
    [string]$GodotPath = 'C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe',
    [ValidateRange(1, 120)]
    [int]$FramesPerSecond = 30,
    [ValidateRange(320, 7680)]
    [int]$Width = 1280,
    [ValidateRange(240, 4320)]
    [int]$Height = 720
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputDir = Join-Path $repoRoot 'output\demo001'
$framesDir = Join-Path $outputDir 'frames'
$moviePath = Join-Path $outputDir 'demo001.avi'
$manifestPath = Join-Path $outputDir 'demo001_manifest.json'

if (-not (Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
    throw "Godot executable not found: $GodotPath"
}

New-Item -ItemType Directory -Force -Path $framesDir | Out-Null
$env:DEMO001_OUTPUT_DIR = $outputDir
$env:DEMO001_MOVIE_FILENAME = 'demo001.avi'
$env:DEMO001_FIXED_FPS = [string]$FramesPerSecond
$env:DEMO001_CAPTURE_WIDTH = [string]$Width
$env:DEMO001_CAPTURE_HEIGHT = [string]$Height
$env:DEMO001_GIT_COMMIT = (git -C $repoRoot rev-parse HEAD).Trim()

try {
    & $GodotPath `
        --path $repoRoot `
        --write-movie $moviePath `
        --fixed-fps $FramesPerSecond `
        --disable-vsync `
        --resolution "${Width}x${Height}" `
        --script res://scripts/demo001_capture.gd
    if ($LASTEXITCODE -ne 0) {
        throw "Demo 001 capture failed with exit code $LASTEXITCODE"
    }
} finally {
    Remove-Item Env:DEMO001_OUTPUT_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:DEMO001_MOVIE_FILENAME -ErrorAction SilentlyContinue
    Remove-Item Env:DEMO001_FIXED_FPS -ErrorAction SilentlyContinue
    Remove-Item Env:DEMO001_CAPTURE_WIDTH -ErrorAction SilentlyContinue
    Remove-Item Env:DEMO001_CAPTURE_HEIGHT -ErrorAction SilentlyContinue
    Remove-Item Env:DEMO001_GIT_COMMIT -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $moviePath -PathType Leaf)) {
    throw "Movie was not produced: $moviePath"
}
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Manifest was not produced: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$movie = Get-Item -LiteralPath $moviePath
$manifest.output | Add-Member -NotePropertyName movie_size_bytes -NotePropertyValue $movie.Length -Force
$manifest.output | Add-Member -NotePropertyName movie_sha256 -NotePropertyValue ((Get-FileHash -LiteralPath $moviePath -Algorithm SHA256).Hash.ToLowerInvariant()) -Force
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host ("DEMO001_CAPTURE_PASS movie={0} bytes={1} manifest={2}" -f $moviePath, $movie.Length, $manifestPath)
