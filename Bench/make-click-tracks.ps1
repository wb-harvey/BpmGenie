<#
.SYNOPSIS
    Builds a folder of click tracks at exact tempos, with a reference CSV, for bpmGenie.Bench.

.DESCRIPTION
    Each track is a 1 kHz click, decaying over about 50 ms, on every beat. The
    CSV is named after the folder so the bench finds it without --reference:
        bpmGenie.Bench "<OutFolder>"

.EXAMPLE
    .\make-click-tracks.ps1 -OutFolder "C:\My Experiments\2026\bpmGenie\TestClicks"
#>
param(
    [string]$OutFolder = "C:\My Experiments\2026\bpmGenie\TestClicks",
    [double]$MinBpm = 60,
    [double]$MaxBpm = 200,
    [double]$Step = 0.5,
    [int]$Seconds = 70,
    [string]$Ffmpeg = "C:\tools\abx\ffmpeg\ffmpeg.exe"
)

$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture

if (-not (Test-Path $Ffmpeg)) { throw "ffmpeg not found: $Ffmpeg" }
New-Item -ItemType Directory -Force $OutFolder | Out-Null

$csv = [System.Collections.Generic.List[string]]::new()
$csv.Add('Name,BPM')

$count = [int][Math]::Floor(($MaxBpm - $MinBpm) / $Step + 1e-9) + 1
for ($i = 0; $i -lt $count; $i++) {
    $bpm = $MinBpm + $i * $Step
    $bpmText = $bpm.ToString('0.0##', $inv)
    $name = 'Click {0} BPM' -f $bpm.ToString('000.0##', $inv)
    $period = (60.0 / $bpm).ToString('R', $inv)

    # Click at the start of every beat: 1 kHz tone with a fast exponential decay
    $expr = "0.8*sin(2*PI*1000*t)*exp(-80*mod(t\,$period))"
    & $Ffmpeg -hide_banner -loglevel error -y -f lavfi -i "aevalsrc=${expr}:s=44100:d=$Seconds" `
        -ac 1 -c:a libmp3lame -b:a 128k (Join-Path $OutFolder "$name.mp3")
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed for $name" }

    $csv.Add("$name,$bpmText")
    Write-Progress -Activity 'Click tracks' -Status $name -PercentComplete (100 * ($i + 1) / $count)
}

$csvPath = Join-Path $OutFolder ((Split-Path $OutFolder -Leaf) + '.csv')
[System.IO.File]::WriteAllLines($csvPath, $csv)
Write-Host "Wrote $count click tracks and $csvPath"
