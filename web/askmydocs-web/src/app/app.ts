import { Component, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AskMyDocsService } from './askmydocs.service';
import { ChatTurn, DocumentSummary } from './models';

type Mode = 'rag' | 'agent' | 'stream';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly api = inject(AskMyDocsService);

  // ---- State (sab signals me - Angular 17+ ka tarika) ----
  readonly documents = signal<DocumentSummary[]>([]);
  readonly turns = signal<ChatTurn[]>([]);
  readonly question = signal('');
  readonly mode = signal<Mode>('stream');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly activeTool = signal<string | null>(null);

  private sessionId: string | null = null;

  readonly canSend = computed(() => this.question().trim().length > 0 && !this.busy());
  readonly totalChunks = computed(() =>
    this.documents().reduce((sum, d) => sum + d.chunkCount, 0));

  readonly samples = [
    'P1 ticket ka SLA kitna hai?',
    'INC-1001 ka status batao',
    'Leave apply karne ka process kya hai?'
  ];

  constructor() {
    void this.refreshDocuments();
  }

  async refreshDocuments(): Promise<void> {
    try {
      this.documents.set(await this.api.listDocuments());
    } catch {
      this.error.set('API se connect nahi ho pa raha. Kya backend chal raha hai (port 5080)?');
    }
  }

  /** File chuno -> text padho -> backend ko bhejo. */
  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    try {
      const content = await file.text();
      const result = await this.api.uploadDocument(file.name, content);
      await this.refreshDocuments();

      this.turns.update(t => [...t, {
        role: 'assistant',
        text: `"${result.fileName}" index ho gaya - ${result.chunkCount} chunks bane. Ab isse sawal pooch sakte ho.`
      }]);
    } catch (e) {
      this.error.set(`Upload fail: ${(e as Error).message}`);
    } finally {
      this.busy.set(false);
      input.value = '';
    }
  }

  async send(): Promise<void> {
    const q = this.question().trim();
    if (!q || this.busy()) {
      return;
    }

    this.question.set('');
    this.error.set(null);
    this.busy.set(true);
    this.turns.update(t => [...t, { role: 'user', text: q }]);

    try {
      switch (this.mode()) {
        case 'rag': await this.sendRag(q); break;
        case 'agent': await this.sendAgent(q); break;
        case 'stream': await this.sendStream(q); break;
      }
    } catch (e) {
      this.error.set((e as Error).message);
    } finally {
      this.busy.set(false);
      this.activeTool.set(null);
    }
  }

  /** Mode 1: plain RAG. Jawab ke saath citations aate hain. */
  private async sendRag(q: string): Promise<void> {
    const res = await this.api.ask(q);
    this.turns.update(t => [...t, {
      role: 'assistant',
      text: res.answer,
      sources: res.sources
    }]);
  }

  /** Mode 2: agent. Tools chalte hain, memory rehti hai. */
  private async sendAgent(q: string): Promise<void> {
    const res = await this.api.askAgent(q, this.sessionId);
    this.sessionId = res.sessionId;
    this.turns.update(t => [...t, {
      role: 'assistant',
      text: res.answer,
      toolsUsed: res.toolsUsed
    }]);
  }

  /** Mode 3: agent + streaming. Token aate hi screen par dikhta hai. */
  private async sendStream(q: string): Promise<void> {
    // Pehle ek khaali bubble daalo, phir usi me text bharte jao.
    this.turns.update(t => [...t, {
      role: 'assistant', text: '', streaming: true, toolsUsed: []
    }]);

    const index = this.turns().length - 1;

    for await (const evt of this.api.streamAgent(q, this.sessionId)) {
      switch (evt.type) {
        case 'session':
          this.sessionId = evt.sessionId;
          break;

        case 'tool':
          this.activeTool.set(evt.name);
          this.patch(index, turn => ({
            ...turn, toolsUsed: [...(turn.toolsUsed ?? []), evt.name]
          }));
          break;

        case 'token':
          this.patch(index, turn => ({ ...turn, text: turn.text + evt.text }));
          break;

        case 'done':
          this.patch(index, turn => ({ ...turn, streaming: false }));
          this.activeTool.set(null);
          break;
      }
    }
  }

  /** Ek hi turn ko immutably update karna - signals ko reference change chahiye. */
  private patch(index: number, fn: (turn: ChatTurn) => ChatTurn): void {
    this.turns.update(turns =>
      turns.map((turn, i) => (i === index ? fn(turn) : turn)));
  }

  clearChat(): void {
    this.turns.set([]);
    this.sessionId = null;
  }

  setMode(mode: Mode): void {
    this.mode.set(mode);
  }

  useSample(s: string): void {
    this.question.set(s);
  }
}
