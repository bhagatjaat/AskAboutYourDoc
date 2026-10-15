import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AgentAskResponse, AskResponse, DocumentSummary, IngestResponse, StreamEvent
} from './models';

const API = 'http://localhost:5080';

@Injectable({ providedIn: 'root' })
export class AskMyDocsService {
  private readonly http = inject(HttpClient);

  /** Document upload - Indexing pipeline trigger hoti hai. */
  uploadDocument(fileName: string, content: string): Promise<IngestResponse> {
    return firstValueFrom(
      this.http.post<IngestResponse>(`${API}/api/documents`, { fileName, content })
    );
  }

  listDocuments(): Promise<DocumentSummary[]> {
    return firstValueFrom(this.http.get<DocumentSummary[]>(`${API}/api/documents`));
  }

  /** Plain RAG - ek hi shot me jawab + citations. */
  ask(question: string): Promise<AskResponse> {
    return firstValueFrom(
      this.http.post<AskResponse>(`${API}/api/ask`, { question })
    );
  }

  /** Agent (non-streaming) - jawab + kaunse tool chale. */
  askAgent(question: string, sessionId: string | null): Promise<AgentAskResponse> {
    return firstValueFrom(
      this.http.post<AgentAskResponse>(`${API}/api/agent/ask`, { question, sessionId })
    );
  }

  /**
   * Agent streaming.
   *
   * HttpClient se SSE padhna aasan nahi hai, kyunki wo poori response
   * aane ka intezaar karta hai. Isliye yahan native fetch + ReadableStream
   * use kiya hai - tokens turant aate hain, ChatGPT jaisa feel.
   *
   * async generator isliye taaki component me seedha
   * `for await (const evt of ...)` likh sakein.
   */
  async *streamAgent(
    question: string,
    sessionId: string | null,
    signal?: AbortSignal
  ): AsyncGenerator<StreamEvent> {
    const response = await fetch(`${API}/api/agent/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question, sessionId }),
      signal
    });

    if (!response.ok || !response.body) {
      throw new Error(`Stream fail: ${response.status}`);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });

      // SSE me har event "data: {...}\n\n" format me aata hai.
      // Aakhri adhoora tukda buffer me rehne do.
      const parts = buffer.split('\n\n');
      buffer = parts.pop() ?? '';

      for (const part of parts) {
        const line = part.trim();
        if (!line.startsWith('data:')) {
          continue;
        }
        yield JSON.parse(line.slice(5).trim()) as StreamEvent;
      }
    }
  }
}
