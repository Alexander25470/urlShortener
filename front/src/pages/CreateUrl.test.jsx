import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import CreateUrl from "../pages/CreateUrl";

vi.mock("../api", () => ({
  fetchJson: vi.fn(),
}));

import { fetchJson } from "../api";

beforeEach(() => {
  vi.clearAllMocks();
});

function renderCreateUrl() {
  return render(
    <MemoryRouter>
      <CreateUrl />
    </MemoryRouter>
  );
}

describe("CreateUrl", () => {
  it("renders the form", () => {
    renderCreateUrl();
    expect(screen.getByText("Nueva URL corta")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /acortar/i })).toBeInTheDocument();
    expect(screen.getByRole("textbox")).toBeInTheDocument();
  });

  it("creates a URL on submit", async () => {
    const user = userEvent.setup();
    fetchJson.mockResolvedValue({ shortUrl: "http://localhost:8080/abc1234" });

    renderCreateUrl();

    await user.type(screen.getByRole("textbox"), "https://example.com/long");
    await user.click(screen.getByRole("button", { name: /acortar/i }));

    expect(fetchJson).toHaveBeenCalledWith("/api/v1/data/shorten", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ longUrl: "https://example.com/long" }),
    });
    expect(screen.getByText(/http:\/\/localhost:8080\/abc1234/)).toBeInTheDocument();
  });

  it("shows error on failure", async () => {
    const user = userEvent.setup();
    fetchJson.mockRejectedValue(new Error("500 Internal Server Error"));

    renderCreateUrl();

    await user.type(screen.getByRole("textbox"), "https://example.com/fail");
    await user.click(screen.getByRole("button", { name: /acortar/i }));

    expect(screen.getByText(/error/i)).toBeInTheDocument();
  });
});
