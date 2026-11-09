# Third-party notices

FramePlay is free software under the GNU General Public License, version 3 or (at your option) any later version. See `LICENSE`.

It includes or is based on the following works.

## mpv / libmpv (`libmpv-2.dll`)

- Plays all audio and video: mpv v0.42.0-5-g55d3feb45, https://mpv.io
- The Windows build comes from https://github.com/shinchiro/mpv-winbuild-cmake, snapshot `20261108-git-55d3feb45c` (`mpv-dev-x86_64`).
- The DLL includes FFmpeg, libass, libplacebo, x264, x265 and other libraries, so the DLL as a whole is under the GNU GPL. That's version 2 or later, and some of the bundled libraries may require version 3.
- **Source code:**
  - mpv at commit `55d3feb45c`: https://github.com/mpv-player/mpv
  - The build scripts, with the exact version of every bundled library: https://github.com/shinchiro/mpv-winbuild-cmake

## MeltySynth

- Plays MIDI files through a SoundFont.
- https://github.com/sinshu/meltysynth
- MIT License:
  - Copyright (C) 2014 Alex Veltsistas
  - Copyright (C) 2017, 2018 Bernhard Schelling
  - Copyright (C) 2021 Nobuaki Tanaka
  - Based on SFZero, Copyright (C) 2012 Steve Folta (https://github.com/stevefolta/SFZero)

## NAudio

- Sends MIDI audio to Windows.
- https://github.com/naudio/NAudio
- MIT License, © Mark Heath 2023.

## "Classic Station" SoundFont (`Classic_Station.sf2`)

- The default soundfont for MIDI, by Yingchun Soul.
- https://musical-artifacts.com/artifacts/2282
- CC0 1.0 (public domain dedication).

## Media player design by Juxtopposed

- FramePlay's interface layout and icons are adapted from this Figma Community design.
- https://www.figma.com/@juxtopposed
- Creative Commons Attribution 4.0, https://creativecommons.org/licenses/by/4.0/.
- The layout and icons were redrawn and changed for FramePlay.

## MarkNotes themes

The built-in colour themes and the `.mntheme` file format come from MarkNotes, by the same author as FramePlay.

---

## MIT License text (MeltySynth, NAudio)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
