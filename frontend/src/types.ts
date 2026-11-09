export type Session = {
  authenticated: boolean;
  userId?: string;
  name?: string;
  role?: "Candidate" | "Reviewer";
  csrfToken: string;
  serverTime: string;
};
export type Criterion = { id: string; label: string; anchors: string[] };
export type Question = {
  id: string;
  number: number;
  kind: "written" | "oral";
  title: string;
  prompt: string;
  rubric: Criterion[];
  version: string;
};
export type Answer = {
  questionId: string;
  revision: number;
  text: string;
  flagged: boolean;
  recordingId?: string | null;
};
export type Attempt = {
  id: string;
  title: string;
  status: "active" | "submitted";
  startedAt: string;
  deadline: string;
  serverTime: string;
  manualOnly: boolean;
  receipt?: string | null;
  questions: Question[];
  answers: Answer[];
};
export type Assignment = {
  id: string;
  title: string;
  durationMinutes: number;
  attemptId?: string | null;
};
export type Score = { criterionId: string; score: number };
export type EvaluationQuestion = {
  questionId: string;
  status: string;
  transcript?: string;
  suggestion?: {
    criteria: (Score & { evidence: string; rationale: string })[];
    provider: string;
    model: string;
    promptVersion: string;
  };
  review?: {
    criteria: Score[];
    feedback: string;
    internalNotes?: string;
    revision: number;
  };
  error?: string;
};
export type Evaluation = {
  attemptId: string;
  status: string;
  revision?: number;
  questions: EvaluationQuestion[];
  publishedAt?: string;
  feedback?: string;
  total?: number;
  maximum?: number;
};
export type ReviewRow = {
  attemptId: string;
  title: string;
  candidateId: string;
  status: string;
  submittedAt: string;
};
