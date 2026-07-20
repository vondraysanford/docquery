import { useRef, useState } from 'react';
import { uploadDocument, deleteDocument } from '../api';

export default function UploadPanel({ documents, onChanged }) {
  const inputRef = useRef(null);
  const [status, setStatus] = useState(null);
  const [busy, setBusy] = useState(false);

  async function handleFileChange(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    setBusy(true);
    setStatus({ kind: 'info', text: `Uploading ${file.name}…` });
    try {
      const result = await uploadDocument(file);
      setStatus({
        kind: 'ok',
        text: `${result.fileName}: ${result.chunksCreated} chunk${result.chunksCreated === 1 ? '' : 's'} indexed`,
      });
      onChanged();
    } catch (error) {
      setStatus({ kind: 'error', text: error.message });
    } finally {
      setBusy(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  async function handleDelete(documentId) {
    try {
      await deleteDocument(documentId);
      onChanged();
    } catch (error) {
      setStatus({ kind: 'error', text: error.message });
    }
  }

  return (
    <aside className="panel upload-panel">
      <h2>Documents</h2>
      <label className={`upload-button ${busy ? 'disabled' : ''}`}>
        {busy ? 'Uploading…' : 'Upload a document'}
        <input
          ref={inputRef}
          type="file"
          accept=".pdf,.md,.markdown,.txt"
          onChange={handleFileChange}
          disabled={busy}
          hidden
        />
      </label>
      <p className="hint">PDF, Markdown, or plain text</p>
      {status && <p className={`status ${status.kind}`}>{status.text}</p>}
      <ul className="doc-list">
        {documents.map((doc) => (
          <li key={doc.id}>
            <span className="doc-name" title={doc.fileName}>{doc.fileName}</span>
            <button
              className="doc-delete"
              onClick={() => handleDelete(doc.id)}
              title={`Remove ${doc.fileName}`}
            >
              ×
            </button>
          </li>
        ))}
        {documents.length === 0 && <li className="doc-empty">Nothing uploaded yet</li>}
      </ul>
    </aside>
  );
}
