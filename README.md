# Homebrew Karaoke

A karaoke system that uses Deep Learning Models to get lyrics and pitch of any vocal songs.\
Still in very early prototyping stage. Intended to be used with my [Audio-Processing-Toolkit](https://github.com/August13742/Audio-Processing-Utilities) (with in-app communication)

Pitch detection &scoring works but needs a lot of tweaking.

- Scrolling Lyrics
- Pitch Key Adjustments
- Input Pitch Matching (procedurual, quality varies)
- Voice Recording & Playback with stem volume control
- Save to Local as .wav
- Drag-n-Drop Songs to Quick Start

## Demo (as of 2026/2/14, SOUND ON)

https://github.com/user-attachments/assets/e6f9ab1c-94db-4e49-a12e-dac62bc5defc


↑ SongName: [Voyaging Star's Farewell](https://youtube.com/watch?v=0HrdRGuF2Y8)\
~~(I was gonna sing it but Video Capturing failed on my Linux...And somehow this thing did't work properly on Windows)~~

## Demo: Drag-n-Drop Song Importing with [Audio-Processing-Toolkit](https://github.com/August13742/Audio-Processing-Utilities)
**NOTE: Due to Godot Editor (and my skill) limitations, Drag-n-Drop DOES NOT WORK on Linux Wayland**

https://github.com/user-attachments/assets/85d923c6-b2e2-44b8-8031-625c21e34942

↑ SongName: 黒田崇矢 - ばかみたい [Taxi Driver Edition\]

---

Most notorious bug is lyrics hallucination & accuracy.\
Tested QwenASR/Force-Aligner, WhisperX & WhisperASR (and hybrids like Whisper + QwenFA / Whisper + WhisperX-FA),\
turns out Whisper(with very loose parameters) is still best for this. 



However, since the parameter is very forgiving, it will hallucinate lyrics when there is only silence. <- Maybe I can do something to try to solve it?(2026/2/11) <- already implemented (2026 2/14), results should be much better now


---
A lot of the code is written by LLM.

To get [Audio-Processing-Toolkit](https://github.com/August13742/Audio-Processing-Utilities) to work, clone it into this Repo, `uv sync` to get dependencies

First Time Running the pipeline will take a long time (depending on internet speed) to Download Model Weights 
