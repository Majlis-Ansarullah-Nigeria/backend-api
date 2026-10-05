import { render, screen } from "@testing-library/react";
import { CheckCircle2 } from "lucide-react";
import { describe, expect, it } from "vitest";
import { EmptyState } from "./EmptyState";

describe("EmptyState", () => {
  it("renders the title and description", () => {
    render(
      <EmptyState
        icon={CheckCircle2}
        title="You're all caught up"
        description="No reports are due right now."
      />,
    );

    expect(screen.getByRole("heading", { name: "You're all caught up" })).toBeInTheDocument();
    expect(screen.getByText("No reports are due right now.")).toBeInTheDocument();
  });

  it("renders an optional action", () => {
    render(<EmptyState icon={CheckCircle2} title="Empty" action={<button type="button">View history</button>} />);
    expect(screen.getByRole("button", { name: "View history" })).toBeInTheDocument();
  });
});
