import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import Layout from "./Layout";
import UrlList from "./pages/UrlList";
import TopUrls from "./pages/TopUrls";
import CreateUrl from "./pages/CreateUrl";

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Navigate to="/urls" replace />} />
          <Route path="urls" element={<UrlList />} />
          <Route path="top" element={<TopUrls />} />
          <Route path="new" element={<CreateUrl />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
