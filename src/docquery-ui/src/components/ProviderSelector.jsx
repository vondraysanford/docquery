/**
 * Dropdown for choosing which provider profile answers questions.
 * Unavailable profiles stay visible but disabled, with the API's reason as
 * their tooltip — so "DGX Spark — unavailable" teaches you the tunnel is down
 * rather than silently hiding the option.
 */
export default function ProviderSelector({ providers, selected, onSelect, onRefresh }) {
  const active = providers.find((p) => p.name === selected);

  return (
    <div className="provider-selector">
      <label htmlFor="provider-select">Provider</label>
      <select
        id="provider-select"
        value={selected ?? ''}
        onFocus={onRefresh}
        onChange={(event) => onSelect(event.target.value)}
      >
        {providers.length === 0 && <option value="">Checking…</option>}
        {providers.map((provider) => (
          <option
            key={provider.name}
            value={provider.name}
            disabled={!provider.available}
            title={provider.reason ?? ''}
          >
            {provider.displayName}
            {provider.available ? '' : ' — unavailable'}
          </option>
        ))}
      </select>
      <button
        type="button"
        className="provider-refresh"
        onClick={onRefresh}
        title="Re-check provider availability"
      >
        ⟳
      </button>
      {active &&
        (active.available ? (
          active.hint && <span className="provider-hint">{active.hint}</span>
        ) : (
          <span className="provider-hint provider-warn">{active.reason}</span>
        ))}
    </div>
  );
}
