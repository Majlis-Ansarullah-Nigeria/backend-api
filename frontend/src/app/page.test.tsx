import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it } from "vitest";
import messages from "../../messages/en.json";
import Home from "./page";

describe("Home", () => {
  it("renders the scaffold placeholder", () => {
    render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <Home />
      </NextIntlClientProvider>,
    );

    expect(
      screen.getByRole("heading", { name: messages.Home.title }),
    ).toBeInTheDocument();
  });
});
