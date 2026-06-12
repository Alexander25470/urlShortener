import { NavLink, Outlet } from "react-router-dom";
import "./index.css";

export default function Layout() {
  return (
    <div className="layout">
      <header className="header">
        <h1 className="title">🔗 URL Shortener</h1>
        <nav className="nav">
          <NavLink to="/urls" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
            📋 URLs
          </NavLink>
          <NavLink to="/top" className={({ isActive }) => isActive ? "nav-link active" : "nav-link"}>
            🏆 Top
          </NavLink>
        </nav>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
