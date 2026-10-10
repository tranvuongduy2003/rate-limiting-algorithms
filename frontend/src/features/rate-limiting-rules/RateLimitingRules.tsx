import { useEffect, useRef, useState } from 'react';
import {
  evaluateRateLimitingRule,
  getRateLimitingRules,
  type CallResult,
  type Outcome,
  type RateLimitingRuleDefinition,
} from '../../shared/api';

const fallbackRules: RateLimitingRuleDefinition[] = [
  {
    domain: 'messaging',
    descriptorKey: 'message_type',
    descriptorValue: 'marketing',
    unit: 'day',
    requestsPerUnit: 5,
  },
  {
    domain: 'auth',
    descriptorKey: 'auth_type',
    descriptorValue: 'login',
    unit: 'minute',
    requestsPerUnit: 5,
  },
];

interface RuleRequestEntry extends Omit<Partial<CallResult>, 'outcome'> {
  id: number;
  outcome: Outcome;
}

const formatRule = (rule: RateLimitingRuleDefinition) => `domain: ${rule.domain}
descriptors:
  - key: ${rule.descriptorKey}
    value: ${rule.descriptorValue}
    rate_limit:
      unit: ${rule.unit}
      requests_per_unit: ${rule.requestsPerUnit}`;

const getRuleContent = (rule: RateLimitingRuleDefinition) => {
  if (rule.domain === 'messaging' && rule.descriptorValue === 'marketing') {
    return {
      title: 'Marketing messages',
      description: `The ${rule.descriptorKey} descriptor selects marketing traffic and allows at most ${rule.requestsPerUnit} matching messages each ${rule.unit}.`,
      requestLabel: 'Send message',
    };
  }

  if (rule.domain === 'auth' && rule.descriptorValue === 'login') {
    return {
      title: 'Login attempts',
      description: `The ${rule.descriptorKey} descriptor selects login traffic and allows at most ${rule.requestsPerUnit} matching attempts each ${rule.unit}.`,
      requestLabel: 'Attempt login',
    };
  }

  return {
    title: rule.descriptorValue,
    description: `This rule matches ${rule.descriptorKey}=${rule.descriptorValue} traffic in the ${rule.domain} domain.`,
    requestLabel: 'Send request',
  };
};

interface RuleExampleProps {
  rule: RateLimitingRuleDefinition;
  apiStatus: 'loading' | 'loaded' | 'fallback';
}

function RuleExample({ rule, apiStatus }: RuleExampleProps) {
  const [entries, setEntries] = useState<RuleRequestEntry[]>([]);
  const clientId = useRef(crypto.randomUUID());
  const nextId = useRef(1);
  const content = getRuleContent(rule);
  const apiAvailable = apiStatus === 'loaded';

  const send = async () => {
    const id = nextId.current++;
    setEntries((current) => [...current, { id, outcome: 'pending' as const }].slice(-8));

    const result = await evaluateRateLimitingRule(rule, clientId.current);
    setEntries((current) => current.map((entry) => (entry.id === id ? { id, ...result } : entry)));
  };

  const burst = async () => {
    for (let request = 0; request < rule.requestsPerUnit + 1; request++) {
      await send();
    }
  };

  const resetClient = () => {
    clientId.current = crypto.randomUUID();
    nextId.current = 1;
    setEntries([]);
  };

  return (
    <article className="rule-example">
      <header className="rule-example-header">
        <span className="rule-domain">{rule.domain}</span>
        <h3>{content.title}</h3>
        <span className={`live-badge ${apiAvailable ? '' : 'unavailable'}`}>
          {apiStatus === 'loaded' ? 'Live API' : apiStatus === 'loading' ? 'Connecting' : 'API unavailable'}
        </span>
      </header>

      <pre className="rule-code" aria-label={`${content.title} YAML configuration`}>
        <code>{formatRule(rule)}</code>
      </pre>

      <p>{content.description}</p>
      <dl className="rule-summary">
        <div>
          <dt>Limit</dt>
          <dd>{rule.requestsPerUnit} requests</dd>
        </div>
        <div>
          <dt>Window</dt>
          <dd>1 {rule.unit}</dd>
        </div>
      </dl>

      <div className="rule-playground">
        <div className="rule-controls">
          <button type="button" className="btn btn-primary" onClick={() => void send()} disabled={!apiAvailable}>
            {content.requestLabel}
          </button>
          <button type="button" className="btn" onClick={() => void burst()} disabled={!apiAvailable}>
            Burst ×{rule.requestsPerUnit + 1}
          </button>
          <button type="button" className="btn" onClick={resetClient} disabled={entries.length === 0}>
            New client
          </button>
        </div>

        {entries.length === 0 ? (
          <p className="rule-empty">Send requests to consume this client's configured allowance.</p>
        ) : (
          <ol className="rule-log" aria-label={`${content.title} request results`}>
            {[...entries].reverse().map((entry) => (
              <li key={entry.id}>
                <span>#{entry.id}</span>
                <strong className={`status ${entry.outcome}`}>
                  {entry.outcome === 'pending' ? '…' : (entry.status ?? 'ERR')}
                </strong>
                <span>
                  {entry.remaining === undefined || entry.limit === undefined
                    ? 'Waiting for quota headers'
                    : `${entry.remaining} of ${entry.limit} remaining`}
                </span>
                {entry.retryAfterSeconds !== undefined && <small>Retry in {entry.retryAfterSeconds}s</small>}
              </li>
            ))}
          </ol>
        )}
      </div>
    </article>
  );
}

export function RateLimitingRules() {
  const [rules, setRules] = useState(fallbackRules);
  const [rulesStatus, setRulesStatus] = useState<'loading' | 'loaded' | 'fallback'>('loading');

  useEffect(() => {
    const controller = new AbortController();

    void getRateLimitingRules(controller.signal)
      .then((configuredRules) => {
        setRules(configuredRules);
        setRulesStatus('loaded');
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }

        setRulesStatus('fallback');
      });

    return () => controller.abort();
  }, []);

  return (
    <section className="rules-section" aria-labelledby="rate-limiting-rules-title">
      <div className="section-heading">
        <p className="section-kicker">Configuration</p>
        <h2 id="rate-limiting-rules-title">Rate limiting rules</h2>
        <p>
          A rate limiter needs rules that describe which traffic to match and how much of it to allow. These live
          examples are loaded from server configuration and enforced per client by the API.
        </p>
      </div>

      <div className="rule-anatomy" aria-label="Anatomy of a rate limiting rule">
        <div>
          <code>domain</code>
          <span>Groups rules by service or use case</span>
        </div>
        <div>
          <code>descriptor</code>
          <span>Matches a specific kind of request</span>
        </div>
        <div>
          <code>rate_limit</code>
          <span>Sets the request budget and time window</span>
        </div>
      </div>

      <div className="rule-examples">
        {rules.map((rule) => (
          <RuleExample
            key={`${rule.domain}:${rule.descriptorKey}:${rule.descriptorValue}`}
            rule={rule}
            apiStatus={rulesStatus}
          />
        ))}
      </div>

      <aside className="rule-note" aria-label="Where rate limiting rules are stored">
        <span aria-hidden="true">i</span>
        <p>
          {rulesStatus === 'loaded' && 'These policies were loaded from the backend configuration file.'}
          {rulesStatus === 'loading' && 'Loading policies from the backend configuration file…'}
          {rulesStatus === 'fallback' &&
            'The backend configuration could not be reached, so the documented fallback policies are shown.'}
        </p>
      </aside>

      <div className="limiting-behavior">
        <article className="behavior-card" aria-labelledby="exceeding-limit-title">
          <div className="behavior-heading">
            <span className="status-code" aria-hidden="true">
              429
            </span>
            <div>
              <p className="section-kicker">Server behavior</p>
              <h3 id="exceeding-limit-title">Exceeding the rate limit</h3>
            </div>
          </div>

          <p>
            When a request exceeds its allowance, the API responds with{' '}
            <code>429 Too Many Requests</code>. Work that must not be lost can be handed to a queue for later
            processing; other requests are rejected immediately.
          </p>

          <ol className="rejection-flow" aria-label="Rate-limited request flow">
            <li>
              <span>1</span>
              <div>
                <strong>Match a rule</strong>
                <small>The API uses the domain, descriptor, and client identity.</small>
              </div>
            </li>
            <li>
              <span>2</span>
              <div>
                <strong>Consume the allowance</strong>
                <small>Accepted requests reduce the remaining count for this window.</small>
              </div>
            </li>
            <li>
              <span>3</span>
              <div>
                <strong>Return HTTP 429</strong>
                <small>The next request is rejected after the allowance reaches zero.</small>
              </div>
            </li>
          </ol>
        </article>

        <article className="behavior-card" aria-labelledby="rate-limiter-headers-title">
          <div className="behavior-heading">
            <span className="header-icon" aria-hidden="true">
              H
            </span>
            <div>
              <p className="section-kicker">Client feedback</p>
              <h3 id="rate-limiter-headers-title">Rate limiter headers</h3>
            </div>
          </div>

          <p>Every evaluated response reports the active quota. Rejected responses also say when to retry.</p>

          <dl className="header-reference">
            <div>
              <dt>
                <code>X-RateLimit-Remaining</code>
              </dt>
              <dd>Requests still available in the current window.</dd>
            </div>
            <div>
              <dt>
                <code>X-RateLimit-Limit</code>
              </dt>
              <dd>Total calls the client can make per window.</dd>
            </div>
            <div>
              <dt>
                <code>X-RateLimit-Retry-After</code>
              </dt>
              <dd>Seconds to wait before another request can succeed.</dd>
            </div>
          </dl>

          <div className="response-example" aria-label="Example rate-limited HTTP response">
            <div className="response-example-title">
              <span>Rate-limited response</span>
              <strong>429</strong>
            </div>
            <code>
              HTTP/1.1 429 Too Many Requests
              <br />
              X-RateLimit-Limit: 5
              <br />
              X-RateLimit-Remaining: 0
              <br />
              X-RateLimit-Retry-After: 42
            </code>
          </div>
        </article>
      </div>
    </section>
  );
}
