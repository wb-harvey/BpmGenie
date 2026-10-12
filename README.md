BPM Scanner (MIT)
=================

BPM Scanner is an open-source windows program designed to automatically detect the tempo (Beats Per Minute) of audio files. 
It is released under the MIT License.

FEATURES
--------
* Free software
* Very high-accuracy BPM detection
* Inspired by the digital signal processing (DSP) approach of MIXXX (https://mixxx.org/); no MIXXX code is used
** Uses FFmpeg to decode the first 60 seconds into mono 11025Hz PCM data
** Computes the Short-Time Fourier Transform (STFT)
** Uses complex domain onset detection (change in magnitude and phase) to find beat onsets
** Autocorrelates the onsets over 60-200 BPM, with log-normal prior weighting around 120 BPM
** Refines the beat period to a fraction of a frame, so the BPM isn't limited to coarse steps (2.0)
*** Uses FFmpeg to read files (ffmpeg.org), LGPL
*** Uses NWAVES to analyze audio (https://github.com/ar1st0crat/NWaves), MIT
*** Uses Taglib# to read/write ID3 tags (https://github.com/mono/taglib-sharp), LGPL
*** Open-source under the MIT License.

INSTALLATION
------------
There is no install package. 
You can download the compiled executable and supporting libraries from http://williamharvey.com/BpmGenie

USAGE
-----
Run the program. Bypass the stupid Windows smartscreen warnings, because it has no certificate.
1. Click Select Folder
2. Navigate to the folder where you keep your MP3 files.
3. Click Select Folder and wait for it to scan
4. Click Apply to save BPM values to ID3 tags
Close or repeat

LIBRARY

BpmGenie.Core (BpmGenie.Core.dll, net10.0, MIT) holds the BPM analysis (bpmGenie.BpmAnalyzerV2).
bpmGenie and the bench use it, and other programs can too: decode the audio to mono float samples at
BpmAnalyzerV2.SampleRate (11025 Hz) and call BpmAnalyzerV2.Analyze(samples, cancellationToken), which returns
the BPM rounded to a whole number, or 0 for silence or no beat. It needs NWaves.dll (MIT) next to it.

TESTING
-------
Bench\bpmGenie.Bench is a console program that runs the 1.0 analysis (BpmAnalyzerV1) and the
2.0 analysis (BpmAnalyzerV2) over a folder of MP3s and compares them. It never writes to the files.
  bpmGenie.Bench <song folder> [--reference <csv>] [--out <folder>] [--limit <n>]
The reference CSV has one Name,BPM row per song, where Name is the file name without .mp3.
It writes results.csv (one row per song) and summary.txt (V1 vs V2 agreement and accuracy).

LICENSE
-------
Copyright (c) 2026 William Harvey

bpmGenie is released under the MIT License: you may use, copy, modify, merge, publish, distribute, sublicense and/or sell copies of it, provided the copyright notice and permission notice are included. It is provided "as is", without warranty of any kind. See the LICENSE file for the full text.

bpmGenie ships with NWaves (MIT), TagLib# (LGPL 2.1) and an LGPL build of FFmpeg 9.0.2, each under its own license. The release's licenses folder has their notices and license texts (THIRD-PARTY-NOTICES.txt).

CONTRIBUTING
------------
Contributions are welcome! Check the project out on GitHib.

CONTACT
-------
Maintainer: junkmail@williamharvey.com
Project Link: https://github.com/wb-harvey/BpmGenie
