#!/usr/bin/env python3
"""Real local API checks. Creates isolated fictional assignments; never rewrites seed data."""
from concurrent.futures import ThreadPoolExecutor
import http.cookiejar
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts'))
from lab import compose

BASE = os.environ.get('LAB_URL', 'http://127.0.0.1:5188').rstrip('/')
AUDIO = ROOT / 'tests/Integration/assets/synthetic-oral.webm'
TEXT = 'I would confirm the facts, explain the impact, agree an owner and next step, and check the result with the team.'
INTERNAL = 'PRIVATE synthetic reviewer note; must never reach a candidate.'


def db(sql):
    result = compose('exec', '-T', 'postgres', 'psql', '-U', 'assessment', '-d', 'assessment', '-v', 'ON_ERROR_STOP=1', '-At',
                     input=sql, capture_output=True, text=True)
    return result.stdout.strip()


def fresh_assignment(candidate_id, title='Integration synthetic', minutes=20):
    candidate_id = str(uuid.UUID(candidate_id))
    identity = str(uuid.uuid4())
    # The source seed stays unchanged. Only an explicitly synthetic assignment is inserted.
    db(f'''INSERT INTO assessments.assignments ("Id","CandidateId","Title","DurationMinutes","QuestionsJson")
           SELECT '{identity}','{candidate_id}','{title}',{int(minutes)},"QuestionsJson"
           FROM assessments.assignments WHERE "Id"='aaaaaaaa-1111-1111-1111-111111111111';''')
    return identity


class Client:
    def __init__(self):
        self.cookies = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cookies))
        self.csrf = None
        self.session = self.request('GET', '/api/session')[1]
        self.csrf = self.session['csrfToken']

    def request(self, method, path, body=None, expected=200, content_type='application/json', csrf=True, raw=False):
        data = body if isinstance(body, bytes) else json.dumps(body).encode() if body is not None else None
        headers = {'Content-Type': content_type}
        if csrf and self.csrf:
            headers['X-CSRF-TOKEN'] = self.csrf
        request = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
        try:
            with self.opener.open(request, timeout=45) as response:
                status, value = response.status, response.read()
        except urllib.error.HTTPError as error:
            status, value = error.code, error.read()
        choices = expected if isinstance(expected, tuple) else (expected,)
        assert status in choices, f'{method} {path}: expected {choices}, got {status}'
        if raw:
            return status, value
        return status, json.loads(value) if value else None

    def login(self, user):
        self.request('POST', '/api/login', {'email': user['email'], 'password': user['password']})
        self.session = self.request('GET', '/api/session')[1]
        self.csrf = self.session['csrfToken']
        assert self.session['authenticated']
        return self


def users():
    return json.loads((ROOT / '.local/credentials.json').read_text())['users']


def start(client, manual_only=True, title='Integration synthetic'):
    assignment = fresh_assignment(client.session['userId'], title)
    return client.request('POST', f'/api/assignments/{assignment}/start', {'manualOnly': manual_only})[1]


def save(client, attempt, question, text=TEXT, revision=0, recording=None, expected=200):
    return client.request('PUT', f"/api/attempts/{attempt['id']}/answers/{question['id']}",
                          {'text': text, 'revision': revision, 'flagged': False, 'recordingId': recording}, expected=expected)[1]


def upload(client, attempt, question, upload_id=None, audio=None, expected=200):
    return client.request('POST', f"/api/attempts/{attempt['id']}/recordings/{question['id']}/{upload_id or uuid.uuid4()}",
                          AUDIO.read_bytes() if audio is None else audio, content_type='audio/webm', expected=expected)[1]


def wait_for(predicate, seconds=90, description='condition'):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        value = predicate()
        if value:
            return value
        time.sleep(1)
    raise AssertionError(f'Timed out waiting for {description}')


def evaluation(client, attempt):
    return client.request('GET', f"/api/evaluation/{attempt['id']}")[1]


def fill(client, attempt):
    for question in attempt['questions']:
        recording = upload(client, attempt, question)['id'] if question['kind'] == 'oral' else None
        save(client, attempt, question, recording=recording)


def sample():
    candidate = Client().login(users()[0])
    sample_file = ROOT / '.local/sample.json'
    if sample_file.exists():
        previous = json.loads(sample_file.read_text())
        status, _ = candidate.request('GET', f"/api/attempts/{previous['attemptId']}", expected=(200, 404))
        if status == 200:
            print(f"Existing synthetic trace: {previous['attemptId']}; receipt {previous['receipt']}")
            return
    attempt = start(candidate, manual_only=False, title='Learning sample synthetic')
    fill(candidate, attempt)
    receipt = candidate.request('POST', f"/api/attempts/{attempt['id']}/submit", {})[1]
    fd = os.open(sample_file, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, 'w') as output:
        json.dump(receipt, output, indent=2)
    print(f"Synthetic attempt: {attempt['id']}; receipt {receipt['receipt']}. Open the reviewer queue to inspect processing and publish a review.")


def integration():
    credentials = users()
    candidate, other, reviewer = [Client().login(user) for user in credentials]
    anonymous = Client()
    attempt = start(candidate)
    expiry = None
    completed = []
    try:
        written = attempt['questions'][0]
        oral = next(q for q in attempt['questions'] if q['kind'] == 'oral')
        anonymous.request('GET', f"/api/attempts/{attempt['id']}", expected=401)
        other.request('GET', f"/api/attempts/{attempt['id']}", expected=404)
        other.request('PUT', f"/api/attempts/{attempt['id']}/answers/{written['id']}",
                      {'revision': 0, 'text': 'unauthorized', 'flagged': False}, expected=404)
        candidate.request('GET', '/api/reviews', expected=403)
        candidate.request('POST', f"/api/reviews/{attempt['id']}/publish", {'feedback': 'forbidden', 'revision': 0}, expected=403)
        candidate.request('PUT', f"/api/attempts/{attempt['id']}/answers/{written['id']}",
                          {'revision': 0, 'text': 'without CSRF', 'flagged': False}, csrf=False, expected=400)
        completed.append('authentication, ownership, reviewer policy and CSRF')

        with ThreadPoolExecutor(max_workers=2) as pool:
            futures = [pool.submit(save, candidate, attempt, written, text=text, expected=(200, 409)) for text in ['first accepted', 'second accepted']]
            responses = [future.result() for future in futures]
        assert sum('revision' in response for response in responses) == 1
        refreshed = candidate.request('GET', f"/api/attempts/{attempt['id']}")[1]
        answer = next(answer for answer in refreshed['answers'] if answer['questionId'] == written['id'])
        assert answer['revision'] == 1 and answer['text'] in {'first accepted', 'second accepted'}
        save(candidate, attempt, written, text=TEXT, revision=1)
        completed.append('concurrent stale save rejected; server answer survives refresh')

        stable_id = str(uuid.uuid4())
        recording = upload(candidate, attempt, oral, stable_id)
        repeated = upload(candidate, attempt, oral, stable_id)
        assert recording['id'] == repeated['id']
        upload(candidate, attempt, oral, str(uuid.uuid4()), b'not audio', expected=400)
        path = f"/api/attempts/{attempt['id']}/recordings/{recording['id']}"
        assert candidate.request('GET', path, raw=True)[1] == AUDIO.read_bytes()
        other.request('GET', path, expected=404)
        anonymous.request('GET', path, expected=401)
        save(candidate, attempt, oral, text='', recording=recording['id'])
        completed.append('whole-file upload idempotency, media validation and private byte-identical playback')

        compose('stop', 'kafka', capture_output=True)
        try:
            receipts = [candidate.request('POST', f"/api/attempts/{attempt['id']}/submit", {})[1] for _ in range(2)]
            assert db(f'''SELECT count(*) FROM assessments.outbox WHERE "AttemptId"='{attempt['id']}' AND "PublishedAt" IS NULL;''') == '12'
        finally:
            compose('start', 'kafka', capture_output=True)
        completed.append('broker outage preserves committed submission and twelve unpublished intents')
        assert receipts[0] == receipts[1] and receipts[0]['receipt']
        save(candidate, attempt, written, revision=2, expected=409)
        wait_for(lambda: len(json.loads(db(f'''SELECT COALESCE(json_agg("Id"),'[]') FROM evaluation.jobs WHERE "AttemptId"='{attempt['id']}';'''))) == 12,
                 description='twelve durable Kafka jobs')
        private = evaluation(candidate, attempt)
        assert 'revision' not in private
        assert all('transcript' not in q and 'suggestion' not in q and q.get('review') is None for q in private['questions'])
        completed.append('durable idempotent receipt, immutable submission, Kafka intake and private unpublished review')

        metadata = db(f'''SELECT json_build_object('id',"Id",'attemptId',"AttemptId",'questionId',"QuestionId",'correlationId',"CorrelationId")
                         FROM assessments.outbox WHERE "AttemptId"='{attempt['id']}' LIMIT 1;''')
        compose('exec', '-T', 'kafka', '/opt/kafka/bin/kafka-console-producer.sh', '--bootstrap-server', 'localhost:9092',
                '--topic', 'assessment.processing.v1', input=(metadata + '\n') * 2, text=True, capture_output=True)
        time.sleep(2)
        assert db(f'''SELECT count(*) FROM evaluation.jobs WHERE "AttemptId"='{attempt['id']}';''') == '12'
        completed.append('duplicate message delivery produces one durable job per intent')

        reviewer.request('POST', f"/api/reviews/{attempt['id']}/publish", {'feedback': 'premature', 'revision': 0}, expected=409)
        for question in attempt['questions']:
            payload = {'criteria': [{'criterionId': criterion['id'], 'score': 3} for criterion in question['rubric']],
                       'feedback': 'Synthetic feedback.', 'internalNotes': INTERNAL, 'revision': 0}
            reviewer.request('PUT', f"/api/reviews/{attempt['id']}/questions/{question['id']}", payload)
            if question == attempt['questions'][0]:
                reviewer.request('PUT', f"/api/reviews/{attempt['id']}/questions/{question['id']}", payload, expected=409)
        review = evaluation(reviewer, attempt)
        publication = reviewer.request('POST', f"/api/reviews/{attempt['id']}/publish", {'feedback': 'Synthetic published feedback.', 'revision': review['revision']})[1]
        assert publication['total'] == 72 and publication['maximum'] == 120
        published = evaluation(candidate, attempt)
        assert published['feedback'] == 'Synthetic published feedback.' and INTERNAL not in json.dumps(published)
        assert 'suggestion' not in json.dumps(published) and 'transcript' not in json.dumps(published)
        audit = reviewer.request('GET', f"/api/reviews/{attempt['id']}/audit")[1]
        assert len(audit) == 13
        completed.append('review concurrency, complete rubric gate, aggregate score, audit and private published feedback')

        expiry = start(other)
        save(other, expiry, expiry['questions'][0], text='Accepted before deadline')
        db(f'''UPDATE assessments.attempts SET "Deadline"=now()-interval '1 second' WHERE "Id"='{expiry['id']}';''')
        wait_for(lambda: db(f'''SELECT "SubmittedAt" IS NOT NULL FROM assessments.attempts WHERE "Id"='{expiry['id']}';''') == 't',
                 seconds=45, description='background expiry without browser request')
        save(other, expiry, expiry['questions'][0], text='Late unsent work', revision=1, expected=409)
        stored = other.request('GET', f"/api/attempts/{expiry['id']}")[1]
        assert stored['answers'][0]['text'] == 'Accepted before deadline'
        completed.append('server expiry finalizes accepted work while browser is absent; late writes rejected')

        candidate.request('DELETE', f"/api/attempts/{attempt['id']}", expected=202)
        candidate.request('GET', path, expected=404)
        wait_for(lambda: db(f'''SELECT "CleanedAt" IS NOT NULL FROM assessments.attempts WHERE "Id"='{attempt['id']}';''') == 't',
                 seconds=45, description='retryable recording and evidence cleanup')
        reviewer.request('GET', f"/api/evaluation/{attempt['id']}", expected=404)
        completed.append('deletion revokes access immediately and completes durable private evidence cleanup')
    finally:
        for client, item in [(candidate, attempt), (other, expiry)]:
            if item:
                client.request('DELETE', f"/api/attempts/{item['id']}", expected=(202, 404))
    result = {'passed': completed, 'attemptId': attempt['id'], 'checkedAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}
    destination = ROOT / '.local/integration-evidence.json'
    destination.write_text(json.dumps(result, indent=2) + '\n')
    os.chmod(destination, 0o600)
    for check in completed:
        print('PASS ' + check)
    print(f'Integration evidence: {destination}')


if __name__ == '__main__':
    command = sys.argv[1] if len(sys.argv) > 1 else 'integration'
    if command == 'browser-fixture':
        identity = fresh_assignment(users()[0]['id'], title='Browser synthetic')
        print(json.dumps({'assignmentId': identity, 'title': 'Browser synthetic'}))
    elif command == 'sample':
        sample()
    elif command == 'integration':
        integration()
    else:
        raise SystemExit('Usage: journey.py sample|integration|browser-fixture')
