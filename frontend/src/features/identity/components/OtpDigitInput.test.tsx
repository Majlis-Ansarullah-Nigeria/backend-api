import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { OtpDigitInput } from "./OtpDigitInput";

function Controlled({ onChange }: { onChange: (value: string) => void }) {
  return <OtpDigitInput value="" onChange={onChange} aria-label="Code" />;
}

describe("OtpDigitInput", () => {
  it("renders six empty digit boxes by default", () => {
    render(<OtpDigitInput value="" onChange={vi.fn()} aria-label="Code" />);

    expect(screen.getAllByRole("textbox")).toHaveLength(6);
  });

  it("shows the current value split across boxes", () => {
    render(<OtpDigitInput value="482" onChange={vi.fn()} aria-label="Code" />);

    const boxes = screen.getAllByRole("textbox") as HTMLInputElement[];
    expect(boxes.map((b) => b.value)).toEqual(["4", "8", "2", "", "", ""]);
  });

  it("advances focus to the next box after typing a digit", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled onChange={onChange} />);

    const boxes = screen.getAllByRole("textbox");
    await user.type(boxes[0]!, "4");

    expect(onChange).toHaveBeenCalledWith("4");
  });

  it("spreads a pasted multi-digit value across the remaining boxes", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled onChange={onChange} />);

    const boxes = screen.getAllByRole("textbox");
    await user.click(boxes[0]!);
    await user.paste("483921");

    expect(onChange).toHaveBeenCalledWith("483921");
  });

  it("strips non-digit characters instead of accepting them", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled onChange={onChange} />);

    const boxes = screen.getAllByRole("textbox");
    await user.type(boxes[0]!, "a");

    // The letter is stripped to an empty digit, not written into the box.
    expect(onChange).toHaveBeenCalledWith("");
  });

  it("disables every box when disabled", () => {
    render(<OtpDigitInput value="" onChange={vi.fn()} disabled aria-label="Code" />);

    for (const box of screen.getAllByRole("textbox")) {
      expect(box).toBeDisabled();
    }
  });
});
