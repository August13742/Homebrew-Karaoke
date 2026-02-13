# Homebrew Karaoke

A karaoke system that uses Deep Learning Models to get lyrics and pitch of any vocal songs.\
Still in very early prototyping stage. Intended to be used with my [Audio-Processing-Toolkit](https://github.com/August13742/Audio-Processing-Utilities) (with in-app communication)

Pitch detection &scoring works but needs a lot of tweaking.

## Demo (as of 2026/2/11, SOUND ON)

https://github.com/user-attachments/assets/7647f468-bc34-4952-8e01-b1e27aeab49e

↑ SongName: [Voyaging Star's Farewell](https://youtube.com/watch?v=0HrdRGuF2Y8)\
~~(I was gonna sing it but Video Capturing failed on my Linux...And somehow this thing did't work properly on Windows)~~

---

currently full of hard-coded paths and bugs (such as pitch note audio desync, pitch detection being too strict, etc.)

Most notorious bug is lyrics hallucination & accuracy.\
Tested QwenASR/Force-Aligner, WhisperX & WhisperASR (and hybrids like Whisper + QwenFA / Whisper + WhisperX-FA),\
turns out Whisper(with very loose parameters) is still best for this. 

However, since the parameter is very forgiving, it will hallucinate lyrics when there is only silence. <- Maybe I can do something to try to solve it?

---
A lot of the code is written by LLM.
