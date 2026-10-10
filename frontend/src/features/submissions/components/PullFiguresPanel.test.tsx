import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import type { PullContextView } from "@/lib/api/generated/hRSApiV1.schemas";
import { PullFiguresPanel } from "./PullFiguresPanel";

const BASE_CONTEXT: PullContextView = {
  eligible: true,
  childLevelName: "Muqam",
  childLevelPluralName: "Muqamaat",
  children: [
    { unitId: "m1", unitName: "Agodi", status: 2 }, // Draft — not pull-eligible
    { unitId: "m2", unitName: "Bodija", status: 4 }, // Submitted
    { unitId: "m3", unitName: "Challenge", status: 5 }, // SubmittedLate
  ],
  summary: null,
};

function renderPanel(props: Partial<React.ComponentProps<typeof PullFiguresPanel>> = {}) {
  const onPull = vi.fn();
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <PullFiguresPanel pullContext={BASE_CONTEXT} isPending={false} onPull={onPull} {...props} />
    </NextIntlClientProvider>,
  );
  return { onPull };
}

describe("PullFiguresPanel", () => {
  it("defaults to every submitted child checked and the Draft one unchecked and disabled", () => {
    renderPanel();

    expect(screen.getByRole("checkbox", { name: "Bodija" })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Challenge" })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Agodi" })).not.toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Agodi" })).toBeDisabled();
  });

  it("shows the included/total count", () => {
    renderPanel();

    expect(screen.getByText("2 / 3")).toBeInTheDocument();
  });

  it("deselecting a submitted child excludes it from the next pull call", async () => {
    const user = userEvent.setup();
    const { onPull } = renderPanel();

    await user.click(screen.getByRole("checkbox", { name: "Bodija" }));
    await user.click(screen.getByRole("button", { name: /pull figures/i }));

    expect(onPull).toHaveBeenCalledWith(["m3"], "");
  });

  it("passes the typed override note on pull", async () => {
    const user = userEvent.setup();
    const { onPull } = renderPanel();

    await user.type(screen.getByLabelText(/note on overrides/i), "Adjusted for a Dila-level meeting");
    await user.click(screen.getByRole("button", { name: /pull figures/i }));

    expect(onPull).toHaveBeenCalledWith(["m2", "m3"], "Adjusted for a Dila-level meeting");
  });

  it("shows 'Pull again' and the summary line once a pull has already happened", () => {
    renderPanel({
      pullContext: {
        ...BASE_CONTEXT,
        summary: {
          pulledAt: "2026-10-01T00:00:00Z",
          includedUnitIds: ["m2", "m3"],
          missingUnitIds: ["m1"],
          overrideNote: null,
          fields: [],
        },
      },
    });

    expect(screen.getByRole("button", { name: /pull again/i })).toBeInTheDocument();
    expect(screen.getByText(/figures pulled from 2 submitted reports/i)).toBeInTheDocument();
    expect(screen.getByText(/1 haven't submitted yet/i)).toBeInTheDocument();
  });

  it("shows the mutation error message when present", () => {
    renderPanel({ error: "Someone else has saved changes to this report." });

    expect(screen.getByText("Someone else has saved changes to this report.")).toBeInTheDocument();
  });
});
