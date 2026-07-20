import { useEffect, useRef, useState } from 'react';
import { askQuestion } from '../api';

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
    setMessages((prev) => [...prev, { role: 'user', content: trimmed }]);
    setBusy(true);
    try {
      const result = await askQuestion(trimmed, conversationId);
      setConversationId(result.conversationId);
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: result.answer, sources: result.sources },
      ]);
    } catch (error) {
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: `Something went wrong: ${error.message}`, isError: true },
      ]);
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
            className={`message ${message.role} ${message.isError ? 'error' : ''}`}
          >
            {message.content}
          </div>
        ))}
        {busy && <div className="message assistant pending">Thinking…</div>}
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
