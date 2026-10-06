import { FixedWindowCounter } from './features/fixed-window-counter/FixedWindowCounter';
import { LeakingBucket } from './features/leaking-bucket/LeakingBucket';
import { SlidingWindowCounter } from './features/sliding-window-counter/SlidingWindowCounter';
import { SlidingWindowLog } from './features/sliding-window-log/SlidingWindowLog';
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
        <LeakingBucket />
        <FixedWindowCounter />
        <SlidingWindowLog />
        <SlidingWindowCounter />
      </main>
    </div>
  );
}

export default App;
