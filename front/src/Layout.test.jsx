import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import Layout from "./Layout";

describe("Layout", () => {
  it("renders title and nav links", () => {
    render(
      <MemoryRouter>
        <Layout />
      </MemoryRouter>
    );
    expect(screen.getByText((c) => c.includes("URL Shortener"))).toBeInTheDocument();
    expect(screen.getByText((c) => c.includes("URLs"))).toBeInTheDocument();
    expect(screen.getByText((c) => c.includes("Top"))).toBeInTheDocument();
  });
});
