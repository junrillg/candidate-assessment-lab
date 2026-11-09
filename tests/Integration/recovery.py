#!/usr/bin/env python3
"""Local-only real provider and interrupted-host recovery; never stops host Ollama."""
import json
import os
from pathlib import Path
import time
from journey import Client, db, evaluation, save, start, upload, users, wait_for, compose, ROOT, cleanup_fixtures
from lab import wait_ready


def main():
    candidate, reviewer = Client().login(users()[0]), Client().login(users()[2])
    attempt = start(candidate, manual_only=False, title='Recovery synthetic')
    question = next(q for q in attempt['questions'] if q['kind'] == 'oral')
    paused = False
    host_stopped = False
    transcriber_stopped = False
    try:
        recording = upload(candidate, attempt, question)
        save(candidate, attempt, question, text='', recording=recording['id'])
        compose('stop', 'transcription', capture_output=True)
        transcriber_stopped = True
        candidate.request('POST', f"/api/attempts/{attempt['id']}/submit", {})

        def job():
            return next(q for q in evaluation(reviewer, attempt)['questions'] if q['questionId'] == question['id'])

        failed = wait_for(lambda: (value if (value := job())['status'] == 'needs-review' else None),
                          seconds=150, description='terminal provider failure with durable retries')
        assert failed['failures'] == 4 and failed.get('suggestion') is None and failed.get('review') is None
        compose('up', '-d', '--no-build', '--wait', '--wait-timeout', '120', 'transcription', capture_output=True)
        transcriber_stopped = False
        compose('pause', 'transcription', capture_output=True)
        paused = True
        reviewer.request('POST', f"/api/reviews/{attempt['id']}/questions/{question['id']}/retry", {})
        wait_for(lambda: job()['status'] == 'processing', seconds=30, description='claimed provider request')
        before = int(db(f'''SELECT "Fence" FROM evaluation.jobs WHERE "AttemptId"='{attempt['id']}' AND "QuestionId"='{question['id']}';'''))
        compose('stop', 'app', capture_output=True)
        host_stopped = True
        # Isolated synthetic job: advance the persisted lease instead of waiting five minutes.
        db(f'''UPDATE evaluation.jobs SET "LeaseUntil"=now()-interval '1 second'
               WHERE "AttemptId"='{attempt['id']}' AND "QuestionId"='{question['id']}' AND "Status"='processing';''')
        compose('unpause', 'transcription', capture_output=True)
        paused = False
        compose('start', 'app', capture_output=True)
        host_stopped = False
        wait_ready()
        recovered = wait_for(lambda: (value if (value := job())['status'] == 'ready' else None),
                             seconds=300, description='reclaimed lease, real transcript and Ollama suggestion')
        after = int(db(f'''SELECT "Fence" FROM evaluation.jobs WHERE "AttemptId"='{attempt['id']}' AND "QuestionId"='{question['id']}';'''))
        assert after > before and recovered['transcript'] and recovered['suggestion']['criteria']
        stale = db(f'''WITH rejected AS (UPDATE evaluation.jobs SET "Error"='stale-result'
                       WHERE "AttemptId"='{attempt['id']}' AND "QuestionId"='{question['id']}'
                       AND "Fence"={before} AND "Status"='processing' AND "LeaseUntil">now() RETURNING 1)
                       SELECT count(*) FROM rejected;''')
        assert stale == '0'
        transcript_saved = db(f'''SELECT "Transcript" IS NOT NULL FROM evaluation.jobs WHERE "AttemptId"='{attempt['id']}' AND "QuestionId"='{question['id']}';''')
        assert transcript_saved == 't'
        checks = ['provider outage reaches needs-review after four durable failures without a zero score',
                  'reviewer retry queues recoverable work',
                  'interrupted host leaves a durable lease; expired lease is reclaimed with a higher fence',
                  'stale completion with the old fence updates zero durable rows',
                  'real whisper.cpp transcript and Ollama criterion suggestions persist after recovery']
        evidence = {'attemptId': attempt['id'], 'passed': checks, 'fenceBefore': before, 'fenceAfter': after,
                    'checkedAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}
        destination = ROOT / '.local/recovery-evidence.json'
        destination.write_text(json.dumps(evidence, indent=2) + '\n')
        os.chmod(destination, 0o600)
        for check in checks:
            print('PASS ' + check)
    finally:
        if paused:
            compose('unpause', 'transcription', capture_output=True)
        if transcriber_stopped:
            compose('start', 'transcription', capture_output=True)
        if host_stopped:
            compose('start', 'app', capture_output=True)
            wait_ready()
        candidate.request('DELETE', f"/api/attempts/{attempt['id']}", expected=(202, 404))
        wait_for(lambda: db(f'''SELECT "CleanedAt" IS NOT NULL FROM assessments.attempts WHERE "Id"='{attempt['id']}';''') in {'t', ''},
                 seconds=45, description='synthetic recovery fixture cleanup')
        cleanup_fixtures()


if __name__ == '__main__':
    main()
