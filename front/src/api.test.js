import { describe, it, expect, vi, beforeEach } from "vitest";
import { fetchJson } from "./api";

beforeEach(() => {
  globalThis.fetch = vi.fn();
});

describe("fetchJson", () => {
  it("returns JSON on success", async () => {
    globalThis.fetch.mockResolvedValue({
      ok: true,
      json: async () => [{ shortCode: "0000001" }],
    });
    const data = await fetchJson("/api/v1/urls");
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "http://localhost:8080/api/v1/urls",
      undefined
    );
    expect(data).toEqual([{ shortCode: "0000001" }]);
  });

  it("throws on HTTP error", async () => {
    globalThis.fetch.mockResolvedValue({
      ok: false,
      status: 500,
      statusText: "Internal Server Error",
    });
    await expect(fetchJson("/api/v1/urls")).rejects.toThrow("500");
  });
});
