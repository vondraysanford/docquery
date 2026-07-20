import { useCallback, useEffect, useState } from 'react';
import UploadPanel from './components/UploadPanel';
import Chat from './components/Chat';
import SourcesPane from './components/SourcesPane';
import { listDocuments } from './api';

export default function App() {
  const [documents, setDocuments] = useState([]);
  const [messages, setMessages] = useState([]);
  const [conversationId, setConversationId] = useState(null);
  const [apiError, setApiError] = useState(null);

  const refreshDocuments = useCallback(async () => {
    try {
      setDocuments(await listDocuments());
      setApiError(null);
    } catch {
      setApiError('Cannot reach the DocQuery API. Is the backend running?');
    }
  }, []);

  useEffect(() => {
    refreshDocuments();
  }, [refreshDocuments]);

  // The sources pane always shows the grounding for the latest answer.
  const latestAnswer = [...messages].reverse().find((m) => m.role === 'assistant');

  return (
    <div className="app">
      <header className="app-header">
        <h1>📄 DocQuery</h1>
        <p>Ask questions about your documents — every answer grounded in cited sources.</p>
      </header>
      {apiError && <div className="api-error">{apiError}</div>}
      <main className="app-layout">
        <UploadPanel documents={documents} onChanged={refreshDocuments} />
        <Chat
          messages={messages}
          setMessages={setMessages}
          conversationId={conversationId}
          setConversationId={setConversationId}
          hasDocuments={documents.length > 0}
        />
        <SourcesPane sources={latestAnswer?.sources ?? []} />
      </main>
    </div>
  );
}
