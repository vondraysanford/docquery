import { useEffect, useRef, useState } from 'react';
import { askQuestionStream } from '../api';

export default function Chat({ messages, setMessages, conversationId, setConversationId, hasDocuments, displayNameFor, presetQuestions = [], demoMode = false }) {
  const [question, setQuestion] = useState('');
  const [busy, setBusy] = useState(false);
  const scrollRef = useRef(null);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages]);

  async function handleSubmit(event) {
    event.preventDefault();
    await ask(question);
  }

  async function ask(text) {
    const trimmed = text.trim();
    if (!trimmed || busy) return;

    setQuestion('');
    // The empty assistant message is the streaming target: sources land on it
    // as soon as retrieval finishes, then tokens grow its content.
    setMessages((prev) => [
      ...prev,
      { role: 'user', content: trimmed },
      { role: 'assistant', content: '', streaming: true },
    ]);
    setBusy(true);

    const updateStreamingMessage = (updater) =>
      setMessages((prev) => {
        const next = [...prev];
        const last = next[next.length - 1];
        next[next.length - 1] = updater(last);
        return next;
      });

    const startedAt = performance.now();
    try {
      const result = await askQuestionStream(trimmed, conversationId, {
        onSources: (sources) => updateStreamingMessage((m) => ({ ...m, sources })),
        onToken: (t) => updateStreamingMessage((m) => ({ ...m, content: m.content + t })),
      });
      setConversationId(result.conversationId);
      const elapsed = ((performance.now() - startedAt) / 1000).toFixed(1);
      updateStreamingMessage((m) => ({
        ...m,
        streaming: false,
        provider: result.provider,
        elapsed,
      }));
    } catch (error) {
      updateStreamingMessage((m) => ({
        ...m,
        streaming: false,
        isError: true,
        content: m.content || `Something went wrong: ${error.message}`,
      }));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel chat-panel">
      <div className="chat-messages" ref={scrollRef}>
        {messages.length === 0 && (
          <div className="chat-empty">
            <p>
              {demoMode
                ? "Ask about Vondray's experience, projects, and skills — or start with a question below."
                : hasDocuments || presetQuestions.length > 0
                  ? 'Ask a question about the documents.'
                  : 'Upload a document, then ask a question about it.'}
            </p>
            {presetQuestions.length > 0 && (
              <div className="preset-questions">
                {presetQuestions.map((preset) => (
                  <button key={preset} type="button" className="preset-chip" onClick={() => ask(preset)}>
                    {preset}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
        {messages.map((message, index) => (
          <div
            key={index}
            className={`message ${message.role} ${message.isError ? 'error' : ''} ${message.streaming ? 'streaming' : ''}`}
          >
            {message.content || (message.streaming ? 'Thinking…' : message.content)}
            {message.provider && (
              <div className="message-meta">
                {displayNameFor ? displayNameFor(message.provider) : message.provider} · {message.elapsed}s
              </div>
            )}
          </div>
        ))}
      </div>
      <form className="chat-input" onSubmit={handleSubmit}>
        <input
          type="text"
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder={demoMode ? 'Ask about Vondray…' : 'Ask a question…'}
          disabled={busy}
        />
        <button type="submit" disabled={busy || !question.trim()}>
          Ask
        </button>
      </form>
    </section>
  );
}
