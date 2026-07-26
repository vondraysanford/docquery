import { useEffect, useRef, useState } from 'react';
import { askQuestionStream } from '../api';

export default function Chat({ messages, setMessages, conversationId, setConversationId, hasDocuments }) {
  const [question, setQuestion] = useState('');
  const [busy, setBusy] = useState(false);
  const scrollRef = useRef(null);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages]);

  async function handleSubmit(event) {
    event.preventDefault();
    const trimmed = question.trim();
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

    try {
      const result = await askQuestionStream(trimmed, conversationId, {
        onSources: (sources) => updateStreamingMessage((m) => ({ ...m, sources })),
        onToken: (t) => updateStreamingMessage((m) => ({ ...m, content: m.content + t })),
      });
      setConversationId(result.conversationId);
      updateStreamingMessage((m) => ({ ...m, streaming: false }));
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
          <p className="chat-empty">
            {hasDocuments
              ? 'Ask a question about your documents.'
              : 'Upload a document, then ask a question about it.'}
          </p>
        )}
        {messages.map((message, index) => (
          <div
            key={index}
            className={`message ${message.role} ${message.isError ? 'error' : ''} ${message.streaming ? 'streaming' : ''}`}
          >
            {message.content || (message.streaming ? 'Thinking…' : message.content)}
          </div>
        ))}
      </div>
      <form className="chat-input" onSubmit={handleSubmit}>
        <input
          type="text"
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder="Ask a question…"
          disabled={busy}
        />
        <button type="submit" disabled={busy || !question.trim()}>
          Ask
        </button>
      </form>
    </section>
  );
}
