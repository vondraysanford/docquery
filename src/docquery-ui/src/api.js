// All calls use relative /api URLs. In dev, Vite's proxy forwards them to the
// local API; in production builds, VITE_API_BASE_URL prefixes them with the
// deployed API's origin. No URLs are hardcoded in components.
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? '';

async function ensureOk(response) {
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Request failed (${response.status})`);
  }
  return response;
}

export async function listDocuments() {
  const response = await ensureOk(await fetch(`${API_BASE}/api/documents`));
  return response.json();
}

export async function uploadDocument(file) {
  const form = new FormData();
  form.append('file', file);
  const response = await ensureOk(
    await fetch(`${API_BASE}/api/documents/upload`, { method: 'POST', body: form }),
  );
  return response.json();
}

export async function deleteDocument(documentId) {
  await ensureOk(
    await fetch(`${API_BASE}/api/documents/${documentId}`, { method: 'DELETE' }),
  );
}

export async function askQuestion(question, conversationId) {
  const response = await ensureOk(
    await fetch(`${API_BASE}/api/query`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question, conversationId }),
    }),
  );
  return response.json();
}
