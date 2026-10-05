import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ErrorState } from "./ErrorState";

describe("ErrorState", () => {
  it("renders as an alert with the title and description", () => {
    render(<ErrorState title="We couldn't load your reports" description="Check your connection." />);

    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(screen.getByText("We couldn't load your reports")).toBeInTheDocument();
    expect(screen.getByText("Check your connection.")).toBeInTheDocument();
  });

  it("calls onRetry when the retry button is clicked", async () => {
    const onRetry = vi.fn();
    const user = userEvent.setup();

    render(<ErrorState title="Failed" onRetry={onRetry} />);
    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("omits the retry button when onRetry is not provided", () => {
    render(<ErrorState title="Failed" />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
