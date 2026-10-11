import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { PrivateNotesCard } from "./PrivateNotesCard";

const useGetPrivateNotes = vi.fn();
const addPrivateNote = vi.fn();

vi.mock("@/lib/api/generated/observer/observer", () => ({
  useGetPrivateNotes: (...args: unknown[]) => useGetPrivateNotes(...args),
  addPrivateNote: (...args: unknown[]) => addPrivateNote(...args),
  getGetPrivateNotesQueryKey: (unitId: string, reportId: string) => ["getPrivateNotes", unitId, reportId],
}));

function renderCard() {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>
        <PrivateNotesCard unitId="m-1" reportId="ri-1" />
      </QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  useGetPrivateNotes.mockReset();
  addPrivateNote.mockReset();
});

describe("PrivateNotesCard", () => {
  it("lists existing notes with author and date", () => {
    useGetPrivateNotes.mockReturnValue({
      isPending: false,
      isError: false,
      data: [{ id: "n1", authorName: "K. Bello", body: "Attendance up from last month.", createdAt: "2026-10-04T00:00:00Z" }],
    });

    renderCard();

    expect(screen.getByText("Attendance up from last month.")).toBeInTheDocument();
    expect(screen.getByText(/K\. Bello/)).toBeInTheDocument();
  });

  it("disables Save note until something is typed", () => {
    useGetPrivateNotes.mockReturnValue({ isPending: false, isError: false, data: [] });

    renderCard();

    expect(screen.getByRole("button", { name: /save note/i })).toBeDisabled();
  });

  it("saves a new note and clears the draft", async () => {
    useGetPrivateNotes.mockReturnValue({ isPending: false, isError: false, data: [] });
    addPrivateNote.mockResolvedValue({ id: "n2", authorName: "Me", body: "New note", createdAt: "2026-10-05T00:00:00Z" });
    const user = userEvent.setup();

    renderCard();
    await user.type(screen.getByPlaceholderText(/add a private note/i), "New note");
    await user.click(screen.getByRole("button", { name: /save note/i }));

    expect(addPrivateNote).toHaveBeenCalledWith("m-1", "ri-1", { body: "New note" });
    expect(await screen.findByPlaceholderText(/add a private note/i)).toHaveValue("");
  });
});
