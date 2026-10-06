import { useRef, useState, type ReactNode } from 'react';
import { callEndpoint, type Outcome } from './api';

interface RequestEntry {
  id: number;
  offsetMs: number;
  outcome: Outcome;
  status?: number;
  latencyMs?: number;
  retryAfterSeconds?: number;
}

const BURST_COUNT = 10;
const BURST_GAP_MS = 100;
const LOG_ROWS = 10;

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

interface AlgorithmCardProps {
  title: string;
  endpoint: string;
  children?: ReactNode;
}

export function AlgorithmCard({ title, endpoint, children }: AlgorithmCardProps) {
  const [entries, setEntries] = useState<RequestEntry[]>([]);
  const nextId = useRef(0);
  const startedAt = useRef<number | null>(null);

  const send = async () => {
    const id = nextId.current++;
    const now = performance.now();
    if (startedAt.current === null) {
      startedAt.current = now;
    }
    const offsetMs = now - startedAt.current;

    setEntries((prev) => [...prev, { id, offsetMs, outcome: 'pending' as const }].slice(-LOG_ROWS));
    const result = await callEndpoint(endpoint);
    setEntries((prev) => prev.map((entry) => (entry.id === id ? { ...entry, ...result } : entry)));
  };

  const burst = async () => {
    for (let i = 0; i < BURST_COUNT; i++) {
      void send();
      await delay(BURST_GAP_MS);
    }
  };

  const clear = () => {
    setEntries([]);
    startedAt.current = null;
  };

  return (
    <article className="card">
      <div className="card-header">
        <h2>{title}</h2>
        <code>GET {endpoint}</code>
      </div>

      {children}

      <div className="controls">
        <button type="button" className="btn btn-primary" onClick={() => void send()}>
          Send 1
        </button>
        <button type="button" className="btn" onClick={() => void burst()}>
          Burst ×{BURST_COUNT}
        </button>
        <button type="button" className="btn" onClick={clear} disabled={entries.length === 0}>
          Clear
        </button>
      </div>

      {entries.length === 0 ? (
        <p className="empty">No requests yet.</p>
      ) : (
        <ol className="log">
          {[...entries].reverse().map((entry) => (
            <li key={entry.id}>
              <span>+{(entry.offsetMs / 1000).toFixed(2)}s</span>
              <span className={`status ${entry.outcome}`}>
                {entry.outcome === 'pending' ? '…' : (entry.status ?? 'ERR')}
              </span>
              <span>{entry.latencyMs === undefined ? '' : `${Math.round(entry.latencyMs)} ms`}</span>
              <span className="note">
                {entry.retryAfterSeconds === undefined ? '' : `Retry-After ${entry.retryAfterSeconds}s`}
              </span>
            </li>
          ))}
        </ol>
      )}
    </article>
  );
}
