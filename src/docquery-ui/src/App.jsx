import { useCallback, useEffect, useState } from 'react';
import UploadPanel from './components/UploadPanel';
import Chat from './components/Chat';
import SourcesPane from './components/SourcesPane';
import ProviderSelector from './components/ProviderSelector';
import ComparePanel from './components/ComparePanel';
import PortfolioNav from './components/PortfolioNav';
import { listDocuments, getProviders, getConfig, setActiveProfile, checkHealth } from './api';

const PROFILE_STORAGE_KEY = 'docquery-profile';

export default function App() {
  const [documents, setDocuments] = useState([]);
  const [messages, setMessages] = useState([]);
  const [conversationId, setConversationId] = useState(null);
  const [apiError, setApiError] = useState(null);
  const [providers, setProviders] = useState([]);
  const [profile, setProfile] = useState(null);
  const [mode, setMode] = useState('chat');
  const [appConfig, setAppConfig] = useState({ demoMode: false, presetQuestions: [] });
  const [waking, setWaking] = useState(false);

  const refreshDocuments = useCallback(async () => {
    try {
      setDocuments(await listDocuments());
      setApiError(null);
    } catch {
      setApiError('Cannot reach the DocQuery API. Is the backend running?');
    }
  }, []);

  const refreshProviders = useCallback(async () => {
    try {
      setProviders(await getProviders());
    } catch {
      // Keep whatever we had; the api-error banner covers total outage.
    }
  }, []);

  const selectProfile = useCallback(
    (name) => {
      setProfile(name);
      setActiveProfile(name);
      localStorage.setItem(PROFILE_STORAGE_KEY, name);
      // Provider stacks have separate document stores — refresh the list so
      // it reflects the newly selected provider's world.
      refreshDocuments();
    },
    [refreshDocuments],
  );

  // The demo API scales to zero, so the first visitor may catch it asleep.
  // Probe /health before loading data: if it doesn't answer promptly, show a
  // "waking up" notice and keep probing instead of flashing an error at
  // someone whose only crime was arriving first.
  useEffect(() => {
    let cancelled = false;

    const loadAll = () => {
      refreshDocuments();
      refreshProviders();
      getConfig().then(setAppConfig).catch(() => {});
    };

    (async () => {
      if (await checkHealth()) {
        if (!cancelled) loadAll();
        return;
      }
      if (cancelled) return;
      setWaking(true);
      for (let attempt = 0; attempt < 30 && !cancelled; attempt++) {
        await new Promise((resolve) => setTimeout(resolve, 2000));
        if (await checkHealth()) break;
      }
      if (!cancelled) {
        setWaking(false);
        setApiError(null);
        loadAll();
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [refreshDocuments, refreshProviders]);

  // Demo mode reframes the app as "Interview Vondray" — the tab title included.
  useEffect(() => {
    if (appConfig.demoMode) document.title = 'DocQuery — Interview Vondray';
  }, [appConfig.demoMode]);

  // Once providers load, adopt the remembered selection if it's still valid,
  // otherwise the server's default.
  useEffect(() => {
    if (profile || providers.length === 0) return;
    const stored = localStorage.getItem(PROFILE_STORAGE_KEY);
    const candidate =
      providers.find((p) => p.name === stored && p.available) ??
      providers.find((p) => p.isDefault) ??
      providers[0];
    selectProfile(candidate.name);
  }, [providers, profile, selectProfile]);

  const displayNameFor = useCallback(
    (name) => providers.find((p) => p.name === name)?.displayName ?? name,
    [providers],
  );

  // The sources pane always shows the grounding for the latest answer.
  const latestAnswer = [...messages].reverse().find((m) => m.role === 'assistant');

  return (
    <>
      {appConfig.demoMode && <PortfolioNav />}
      <div className={`app ${appConfig.demoMode ? 'with-nav' : ''}`}>
        <header className="app-header">
          <div>
            <h1>
              DocQuery
              {appConfig.demoMode && <span className="demo-badge">Ask my portfolio</span>}
            </h1>
            <p>
              {appConfig.demoMode
                ? 'Ask questions about Vondray — every answer grounded in cited sources.'
                : 'Ask questions about your documents — every answer grounded in cited sources.'}
            </p>
          </div>
          {/* The public demo is single-provider and read-only: no compare mode,
              no provider selector — those are dev/local affordances. */}
          {!appConfig.demoMode && (
            <div className="header-controls">
              <button
                type="button"
                className="mode-toggle"
                onClick={() => {
                  refreshProviders();
                  setMode(mode === 'chat' ? 'compare' : 'chat');
                }}
              >
                {mode === 'chat' ? 'Compare providers' : 'Back to chat'}
              </button>
              {mode === 'chat' && (
                <ProviderSelector
                  providers={providers}
                  selected={profile}
                  onSelect={selectProfile}
                  onRefresh={refreshProviders}
                />
              )}
            </div>
          )}
        </header>
        {waking && (
          <div className="waking-banner">
            Waking the demo up — it runs on a scale-to-zero container, so the first visit
            takes a few seconds<span className="waking-dots" />
          </div>
        )}
        {apiError && !waking && <div className="api-error">{apiError}</div>}
        {mode === 'chat' ? (
          <main className={`app-layout ${appConfig.demoMode ? 'demo-layout' : ''}`}>
            {!appConfig.demoMode && <UploadPanel documents={documents} onChanged={refreshDocuments} />}
            <Chat
              messages={messages}
              setMessages={setMessages}
              conversationId={conversationId}
              setConversationId={setConversationId}
              hasDocuments={documents.length > 0}
              displayNameFor={displayNameFor}
              presetQuestions={appConfig.presetQuestions}
              demoMode={appConfig.demoMode}
            />
            <SourcesPane sources={latestAnswer?.sources ?? []} />
          </main>
        ) : (
          <main className="app-layout compare-layout">
            <ComparePanel providers={providers} displayNameFor={displayNameFor} />
          </main>
        )}
        {appConfig.demoMode && (
          <footer className="demo-footer">
            <span>
              Read-only demo — answers are grounded in Vondray's resume, projects, and
              interview answers.
            </span>
            <nav>
              <a
                href="https://github.com/vondraysanford/docquery"
                target="_blank"
                rel="noreferrer"
              >
                Source on GitHub
              </a>
            </nav>
          </footer>
        )}
      </div>
    </>
  );
}
