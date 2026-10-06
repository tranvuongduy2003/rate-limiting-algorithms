import { FixedWindowCounter } from './features/fixed-window-counter/FixedWindowCounter';
import { RedisFixedWindowCounter } from './features/fixed-window-counter/RedisFixedWindowCounter';
import { LeakingBucket } from './features/leaking-bucket/LeakingBucket';
import { RedisLeakingBucket } from './features/leaking-bucket/RedisLeakingBucket';
import { RedisSlidingWindowCounter } from './features/sliding-window-counter/RedisSlidingWindowCounter';
import { SlidingWindowCounter } from './features/sliding-window-counter/SlidingWindowCounter';
import { RedisSlidingWindowLog } from './features/sliding-window-log/RedisSlidingWindowLog';
import { SlidingWindowLog } from './features/sliding-window-log/SlidingWindowLog';
import { RedisTokenBucket } from './features/token-bucket/RedisTokenBucket';
import { TokenBucket } from './features/token-bucket/TokenBucket';
import './App.css';

function App() {
  return (
    <div className="page">
      <header className="page-header">
        <h1>Rate limiting algorithms</h1>
        <p>
          Each card calls a Minimal API endpoint guarded by a hand-written rate limiter. Rejected requests get{' '}
          <code>429 Too Many Requests</code>.
        </p>
      </header>

      <main className="grid">
        <TokenBucket />
        <RedisTokenBucket />
        <LeakingBucket />
        <RedisLeakingBucket />
        <FixedWindowCounter />
        <RedisFixedWindowCounter />
        <SlidingWindowLog />
        <RedisSlidingWindowLog />
        <SlidingWindowCounter />
        <RedisSlidingWindowCounter />
      </main>
    </div>
  );
}

export default App;
