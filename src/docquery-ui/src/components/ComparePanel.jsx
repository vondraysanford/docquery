import { useState } from 'react';
import { askQuestionStream } from '../api';

const SIDES = ['left', 'right'];

/**
 * Side-by-side comparison: one question fans out to two providers at once,
 * both answers stream in live with their own timing badge and sources.
 * Each side keeps its own conversation, so follow-ups work independently.
 * Shows one exchange at a time — a new question replaces the previous pair.
 */
export default function ComparePanel({ providers, displayNameFor }) {
  const availableNames = providers.filter((p) => p.available).map((p) => p.name);

  const [chosen, setChosen] = useState(() => ({
    left: availableNames[0] ?? '',
    right: availableNames.find((name) => name !== availableNames[0]) ?? '',
  }));
  const [question, setQuestion] = useState('');
  const [asked, setAsked] = useState(null);
  const [busy, setBusy] = useState(false);
  const [answers, setAnswers] = useState({ left: null, right: null });
  const [threads, setThreads] = useState({ left: null, right: null });

  const patch = (side, updater) =>
    setAnswers((prev) => ({ ...prev, [side]: updater(prev[side]) }));

  function selectProvider(side, name) {
    setChosen((prev) => ({ ...prev, [side]: name }));
    // A different provider means a different store and model — start that
    // side's conversation over rather than pretending it has the history.
    setThreads((prev) => ({ ...prev, [side]: null }));
    setAnswers((prev) => ({ ...prev, [side]: null }));
  }

  async function runSide(side, trimmed) {
    const providerName = chosen[side];
    const startedAt = performance.now();
    patch(side, () => ({ content: '', streaming: true, sources: null }));
    try {
      const result = await askQuestionStream(
        trimmed,
        threads[side],
        {
          onSources: (sources) => patch(side, (m) => ({ ...m, sources })),
          onToken: (t) => patch(side, (m) => ({ ...m, content: m.content + t })),
        },
        providerName,
      );
      setThreads((prev) => ({ ...prev, [side]: result.conversationId }));
      patch(side, (m) => ({
        ...m,
        streaming: false,
        provider: result.provider,
        elapsed: ((performance.now() - startedAt) / 1000).toFixed(1),
      }));
    } catch (error) {
      patch(side, (m) => ({
        ...m,
        streaming: false,
        isError: true,
        content: m?.content || `Something went wrong: ${error.message}`,
      }));
    }
  }

  async function handleSubmit(event) {
    event.preventDefault();
    const trimmed = question.trim();
    if (!trimmed || busy) return;

    setQuestion('');
    setAsked(trimmed);
    setBusy(true);
    await Promise.allSettled(SIDES.map((side) => runSide(side, trimmed)));
    setBusy(false);
  }

  if (availableNames.length < 2) {
    return (
      <section className="panel compare-panel">
        <p className="chat-empty">
          Comparing needs at least two available providers. Check the provider
          selector in chat mode to see what's down and why.
        </p>
      </section>
    );
  }

  return (
    <section className="panel compare-panel">
      <div className="compare-columns">
        {SIDES.map((side) => {
          const other = side === 'left' ? 'right' : 'left';
          const message = answers[side];
          return (
            <div key={side} className="compare-side">
              <select
                value={chosen[side]}
                onChange={(event) => selectProvider(side, event.target.value)}
                disabled={busy}
              >
                {providers.map((provider) => (
                  <option
                    key={provider.name}
                    value={provider.name}
                    disabled={!provider.available || chosen[other] === provider.name}
                    title={provider.reason ?? ''}
                  >
                    {provider.displayName}
                    {provider.available ? '' : ' — unavailable'}
                  </option>
                ))}
              </select>
              {asked && <div className="message user compare-question">{asked}</div>}
              {message && (
                <div
                  className={`message assistant ${message.isError ? 'error' : ''} ${message.streaming ? 'streaming' : ''}`}
                >
                  {message.content || (message.streaming ? 'Thinking…' : '')}
                  {message.provider && (
                    <div className="message-meta">
                      {displayNameFor(message.provider)} · {message.elapsed}s
                    </div>
                  )}
                </div>
              )}
              {message?.sources?.length > 0 && (
                <ul className="compare-sources">
                  {message.sources.map((source, index) => (
                    <li key={index}>
                      <span className="source-doc">{source.documentName}</span>
                      <span className="source-score">
                        {Math.round(source.relevanceScore * 100)}%
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          );
        })}
      </div>
      <form className="chat-input" onSubmit={handleSubmit}>
        <input
          type="text"
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder="Ask both providers the same question…"
          disabled={busy}
        />
        <button type="submit" disabled={busy || !question.trim()}>
          Ask both
        </button>
      </form>
    </section>
  );
}
