// All calls use relative /api URLs. In dev, Vite's proxy forwards them to the
// local API; in production builds, VITE_API_BASE_URL prefixes them with the
// deployed API's origin. No URLs are hardcoded in components.
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? '';

// The active provider profile is sent on every request so the API routes it
// to the matching provider stack. Set by the provider selector; null means
// "use the server's default profile".
let activeProfile = null;

export function setActiveProfile(name) {
  activeProfile = name;
}

function withProfile(headers = {}) {
  return activeProfile ? { ...headers, 'X-DocQuery-Profile': activeProfile } : headers;
}

async function ensureOk(response) {
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Request failed (${response.status})`);
  }
  return response;
}

export async function getProviders() {
  const response = await ensureOk(await fetch(`${API_BASE}/api/providers`));
  return response.json();
}

export async function listDocuments() {
  const response = await ensureOk(
    await fetch(`${API_BASE}/api/documents`, { headers: withProfile() }),
  );
  return response.json();
}

export async function uploadDocument(file) {
  const form = new FormData();
  form.append('file', file);
  const response = await ensureOk(
    await fetch(`${API_BASE}/api/documents/upload`, {
      method: 'POST',
      headers: withProfile(),
      body: form,
    }),
  );
  return response.json();
}

export async function deleteDocument(documentId) {
  await ensureOk(
    await fetch(`${API_BASE}/api/documents/${documentId}`, {
      method: 'DELETE',
      headers: withProfile(),
    }),
  );
}

export async function askQuestion(question, conversationId) {
  const response = await ensureOk(
    await fetch(`${API_BASE}/api/query`, {
      method: 'POST',
      headers: withProfile({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ question, conversationId }),
    }),
  );
  return response.json();
}

// Streaming variant: reads the Server-Sent Events response and invokes
// handlers as events arrive — onSources(sources[]) as soon as retrieval
// completes, onToken(text) per answer delta. Resolves with the "done" event
// payload: { conversationId, provider }.
export async function askQuestionStream(question, conversationId, { onSources, onToken } = {}) {
  const response = await ensureOk(
    await fetch(`${API_BASE}/api/query/stream`, {
      method: 'POST',
      headers: withProfile({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ question, conversationId }),
    }),
  );

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let result = {};

  const handleFrame = (frame) => {
    let eventType = 'message';
    const dataLines = [];
    for (const line of frame.split('\n')) {
      if (line.startsWith('event: ')) eventType = line.slice(7).trim();
      else if (line.startsWith('data: ')) dataLines.push(line.slice(6));
    }
    if (dataLines.length === 0) return;
    const payload = JSON.parse(dataLines.join('\n'));
    if (eventType === 'sources') onSources?.(payload);
    else if (eventType === 'token') onToken?.(payload.t);
    else if (eventType === 'done') result = payload;
  };

  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    const frames = buffer.split('\n\n');
    buffer = frames.pop();
    frames.forEach(handleFrame);
  }

  return result;
}
