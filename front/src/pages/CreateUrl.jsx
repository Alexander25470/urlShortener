import { useState } from "react";
import { fetchJson } from "../api";

export default function CreateUrl() {
  const [longUrl, setLongUrl] = useState("");
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError(null);
    setResult(null);
    setLoading(true);
    try {
      const data = await fetchJson("/api/v1/data/shorten", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ longUrl }),
      });
      setResult(data.shortUrl);
      setLongUrl("");
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <h2>Nueva URL corta</h2>
      <form className="create-form" onSubmit={handleSubmit}>
        <input
          className="url-input"
          type="url"
          placeholder="https://ejemplo.com/url-muy-larga"
          value={longUrl}
          onChange={(e) => setLongUrl(e.target.value)}
          required
        />
        <button className="btn" type="submit" disabled={loading || !longUrl}>
          {loading ? "Acortando..." : "Acortar"}
        </button>
      </form>

      {result && (
        <div className="result-box">
          <span className="result-label">URL corta:</span>
          <a className="result-url" href={result} target="_blank" rel="noreferrer">
            {result}
          </a>
        </div>
      )}

      {error && <div className="error">Error: {error}</div>}
    </div>
  );
}
