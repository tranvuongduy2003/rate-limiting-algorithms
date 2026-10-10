export type Outcome = 'pending' | 'ok' | 'limited' | 'error';

export interface CallResult {
  outcome: Exclude<Outcome, 'pending'>;
  status?: number;
  latencyMs: number;
  limit?: number;
  remaining?: number;
  retryAfterSeconds?: number;
}

export interface RateLimitingRuleDefinition {
  domain: string;
  descriptorKey: string;
  descriptorValue: string;
  unit: string;
  requestsPerUnit: number;
}

const readNumberHeader = (response: Response, name: string) => {
  const value = response.headers.get(name);
  if (value === null) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
};

export async function callEndpoint(path: string, init?: RequestInit): Promise<CallResult> {
  const started = performance.now();
  try {
    const response = await fetch(path, { cache: 'no-store', ...init });
    await response.text();

    return {
      outcome: response.ok ? 'ok' : response.status === 429 ? 'limited' : 'error',
      status: response.status,
      latencyMs: performance.now() - started,
      limit: readNumberHeader(response, 'X-RateLimit-Limit'),
      remaining: readNumberHeader(response, 'X-RateLimit-Remaining'),
      retryAfterSeconds:
        readNumberHeader(response, 'X-RateLimit-Retry-After') ?? readNumberHeader(response, 'Retry-After'),
    };
  } catch {
    return { outcome: 'error', latencyMs: performance.now() - started };
  }
}

export async function getRateLimitingRules(signal?: AbortSignal): Promise<RateLimitingRuleDefinition[]> {
  const response = await fetch('/api/rate-limiting-rules', { cache: 'no-store', signal });
  if (!response.ok) {
    throw new Error(`Could not load rate limiting rules (${response.status}).`);
  }

  return response.json() as Promise<RateLimitingRuleDefinition[]>;
}

export function evaluateRateLimitingRule(
  rule: RateLimitingRuleDefinition,
  clientId: string,
): Promise<CallResult> {
  return callEndpoint('/api/rate-limiting-rules/evaluate', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Client-Id': clientId,
    },
    body: JSON.stringify({
      domain: rule.domain,
      descriptorKey: rule.descriptorKey,
      descriptorValue: rule.descriptorValue,
    }),
  });
}
