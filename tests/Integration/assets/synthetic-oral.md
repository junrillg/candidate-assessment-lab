# Synthetic oral fixture

Generated locally with macOS `say`, converted to mono Opus/WebM with FFmpeg. This is fictional test speech; it contains no real candidate or customer information. It is used to exercise upload, transcription and scoring, never as evidence of assessment validity.

Exact speech:

> I would first acknowledge the impact and confirm the facts with the team. I would identify who owns the next step, agree a realistic time, and give the customer a clear update. I would check the result and close the loop.

Regenerate on macOS:

```sh
say -r 155 -o .local/synthetic-oral.aiff 'I would first acknowledge the impact and confirm the facts with the team. I would identify who owns the next step, agree a realistic time, and give the customer a clear update. I would check the result and close the loop.'
ffmpeg -nostdin -y -loglevel error -i .local/synthetic-oral.aiff -vn -ac 1 -ar 16000 -c:a libopus tests/Integration/assets/synthetic-oral.webm
```
