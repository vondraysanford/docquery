export default function SourcesPane({ sources }) {
  return (
    <aside className="panel sources-panel">
      <h2>Sources</h2>
      {sources.length === 0 ? (
        <p className="hint">The chunks that ground each answer appear here.</p>
      ) : (
        <ol className="source-list">
          {sources.map((source, index) => (
            <li key={index} className="source-card">
              <div className="source-head">
                <span className="source-doc" title={source.documentName}>
                  {source.documentName}
                </span>
                <span className="source-score">
                  {(source.relevanceScore * 100).toFixed(0)}%
                </span>
              </div>
              <blockquote>{source.chunkContent}</blockquote>
            </li>
          ))}
        </ol>
      )}
    </aside>
  );
}
