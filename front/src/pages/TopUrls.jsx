import { useEffect, useState } from "react";
import { fetchJson } from "../api";
import ClickChart from "../components/ClickChart";

export default function TopUrls() {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetchJson("/api/v1/analytics/top?limit=10")
      .then(setData)
      .catch((e) => setError(e.message));
  }, []);

  if (error) return <div className="error">Error: {error}</div>;
  if (!data) return <div className="loading">Cargando...</div>;

  const stats = data.data || [];

  return (
    <div>
      <h2>Top URLs</h2>
      <ClickChart
        data={stats.map((s) => ({ timestamp: s.shortCode, count: s.clickCount }))}
        type="bar"
      />
      <table className="table">
        <thead>
          <tr>
            <th>shortCode</th>
            <th>longUrl</th>
            <th>clicks</th>
          </tr>
        </thead>
        <tbody>
          {stats.map((s) => (
            <tr key={s.shortCode}>
              <td>{s.shortCode}</td>
              <td className="url-cell">{s.longUrl}</td>
              <td>{s.clickCount}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
