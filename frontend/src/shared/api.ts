export type Outcome = 'pending' | 'ok' | 'limited' | 'error';

export interface CallResult {
  outcome: Exclude<Outcome, 'pending'>;
  status?: number;
  latencyMs: number;
  retryAfterSeconds?: number;
}

export async function callEndpoint(path: string): Promise<CallResult> {
  const started = performance.now();
  try {
    const response = await fetch(path, { cache: 'no-store' });
    await response.text();
    const retryAfter = response.headers.get('Retry-After');

    return {
      outcome: response.ok ? 'ok' : response.status === 429 ? 'limited' : 'error',
      status: response.status,
      latencyMs: performance.now() - started,
      retryAfterSeconds: retryAfter ? Number(retryAfter) : undefined,
    };
  } catch {
    return { outcome: 'error', latencyMs: performance.now() - started };
  }
}
