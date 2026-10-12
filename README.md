BPM Scanner (GPLv3)
===================

BPM Scanner is an open-source windows program designed to automatically detect the tempo (Beats Per Minute) of audio files. 
It is released under the GNU General Public License v3.0.

FEATURES
--------
* Free software
* Very high-accuracy BPM detection
* Inspired by the digital signal processing (DSP) strategy from MIXXX (https://mixxx.org/), GPL
** Uses FFmpeg to decode the first 60 seconds into mono 11025Hz PCM data
** Computes the Short-Time Fourier Transform (STFT)
** Uses complex domain onset detection (change in magnitude and phase) to find beat onsets
** Autocorrelates the onsets over 60-200 BPM, with log-normal prior weighting around 120 BPM
** Refines the beat period to a fraction of a frame, so the BPM isn't limited to coarse steps (2.0)
*** Uses FFmpeg to read files (ffmpeg.org), LGPL
*** Uses NWAVES to analyze audio (https://github.com/ar1st0crat/NWaves), MIT
*** Uses Taglib# to read/write ID3 tags (https://github.com/mono/taglib-sharp), LGPL
*** Open-source under GPLv3.

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

TESTING
-------
Bench\bpmGenie.Bench is a console program that runs the 1.0 analysis (BpmAnalyzerV1) and the
2.0 analysis (BpmAnalyzerV2) over a folder of MP3s and compares them. It never writes to the files.
  bpmGenie.Bench <song folder> [--reference <csv>] [--out <folder>] [--limit <n>]
The reference CSV has one Name,BPM row per song, where Name is the file name without .mp3.
It writes results.csv (one row per song) and summary.txt (V1 vs V2 agreement and accuracy).

LICENSE
-------
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.

CONTRIBUTING
------------
Contributions are welcome! Check the project out on GitHib.

CONTACT
-------
Maintainer: junkmail@williamharvey.com
Project Link: https://github.com/wb-harvey/BpmGenie
