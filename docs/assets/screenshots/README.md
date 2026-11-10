# App screenshots

These are unedited captures of the real local Candidate Assessment application from the 2026-11-09 delivery review. They show fictional seeded identities and synthetic responses, recordings, feedback and reviewer notes. No real candidate data or credentials are shown.

| Image | Screen | Original capture |
|---|---|---|
| [candidate-workspace.png](candidate-workspace.png) | Written response, timer, accepted server revision and question navigation | `.impeccable/review/desktop.png` |
| [preparation.png](preparation.png) | Instructions and practice tools before the timer starts | `.impeccable/review/preparation-desktop.png` |
| [recording.png](recording.png) | Spoken response with playback and verified server selection | `.impeccable/review/recording-desktop.png` |
| [reviewer-workspace.png](reviewer-workspace.png) | Synthetic spoken evidence, saved Whisper transcript, Ollama suggestion and human rubric scores | `.impeccable/review/ai-reviewer-desktop.png` |

The written, preparation and recording captures come from the isolated Chromium browser journey, which uses generated microphone audio. The reviewer capture shows the retained synthetic learning sample. The frontend source remains the shipped UI at commit `70917af`; the subsequent Azure profile changed infrastructure and documentation only. These images illustrate the interface, not a hiring decision or physical microphone-quality test.

The tracked PNGs are byte-identical to the reviewed captures, each 1440 pixels wide. The original review evidence is also tracked under `.impeccable/review/`; these stable documentation paths make the selected screens easy to find from the README.
