// Backend ke contracts ka Angular version.
// Ye records .NET ke Models/Contracts.cs se exactly match karte hain.

export interface IngestResponse {
  documentId: string;
  fileName: string;
  chunkCount: number;
}

export interface DocumentSummary {
  documentId: string;
  fileName: string;
  chunkCount: number;
  uploadedAt: string;
}

export interface SourceRef {
  marker: number;
  fileName: string;
  chunkIndex: number;
  score: number;
  snippet: string;
}

export interface AskResponse {
  answer: string;
  sources: SourceRef[];
  retrievedCount: number;
}

export interface AgentAskResponse {
  answer: string;
  sessionId: string;
  toolsUsed: string[];
}

/** UI me dikhne wala ek chat bubble. */
export interface ChatTurn {
  role: 'user' | 'assistant';
  text: string;
  sources?: SourceRef[];
  toolsUsed?: string[];
  streaming?: boolean;
}

/** /api/agent/stream se aane wale SSE events. */
export type StreamEvent =
  | { type: 'session'; sessionId: string }
  | { type: 'tool'; name: string }
  | { type: 'token'; text: string }
  | { type: 'done' };
