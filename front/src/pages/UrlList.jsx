import { useEffect, useState } from "react";
import { fetchJson } from "../api";
import ClickChart from "../components/ClickChart";

export default function UrlList() {
  const [urls, setUrls] = useState([]);
  const [error, setError] = useState(null);
  const [expanded, setExpanded] = useState(null);
  const [bucket, setBucket] = useState("day");
  const [clickData, setClickData] = useState(null);

  useEffect(() => {
    fetchJson("/api/v1/urls")
      .then(setUrls)
      .catch((e) => setError(e.message));
  }, []);

  useEffect(() => {
    if (!expanded) {
      setClickData(null);
      return;
    }
    fetchJson(`/api/v1/${expanded}/clicks?bucket=${bucket}`)
      .then((res) => setClickData(res.data || res))
      .catch((e) => setClickData([]));
  }, [expanded, bucket]);

  if (error) return <div className="error">Error: {error}</div>;

  return (
    <div>
      <h2>URLs acortadas</h2>
      <table className="table">
        <thead>
          <tr>
            <th>shortCode</th>
            <th>longUrl</th>
            <th>creado</th>
          </tr>
        </thead>
        <tbody>
          {urls.map((u) => (
            <>
              <tr
                key={u.shortCode}
                className={expanded === u.shortCode ? "row-expanded" : "row-clickable"}
                onClick={() => setExpanded(expanded === u.shortCode ? null : u.shortCode)}
              >
                <td>{u.shortCode}</td>
                <td className="url-cell">{u.longUrl}</td>
                <td>{new Date(u.createdAt).toLocaleDateString()}</td>
              </tr>
              {expanded === u.shortCode && (
                <tr key={`${u.shortCode}-chart`}>
                  <td colSpan={3}>
                    <div className="chart-section">
                      <label>
                        Bucket:
                        <select value={bucket} onChange={(e) => setBucket(e.target.value)}>
                          <option value="day">day</option>
                          <option value="hour">hour</option>
                          <option value="minute">minute</option>
                        </select>
                      </label>
                      <ClickChart data={clickData || []} type="line" />
                    </div>
                  </td>
                </tr>
              )}
            </>
          ))}
        </tbody>
      </table>
    </div>
  );
}
