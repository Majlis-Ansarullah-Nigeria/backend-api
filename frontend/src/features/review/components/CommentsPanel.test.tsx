import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { CommentsPanel } from "./CommentsPanel";

const useGetReportComments = vi.fn();
const useAddReportComment = vi.fn();

vi.mock("@/lib/api/generated/submissions/submissions", () => ({
  useGetReportComments: (...args: unknown[]) => useGetReportComments(...args),
  useAddReportComment: (...args: unknown[]) => useAddReportComment(...args),
  getGetReportCommentsQueryKey: (id: string) => ["getReportComments", id],
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const FIELDS = [{ fieldKey: "tajnid", label: "Tajnid" }];

const THREAD = {
  fieldKey: "tajnid",
  fieldLabel: "Tajnid",
  root: { id: "c1", authorUserId: "u1", authorName: "Secretary Jane", body: "Can you clarify this figure?", createdAt: "2026-10-01T10:00:00Z" },
  replies: [
    { id: "c2", authorUserId: "u2", authorName: "Head Bello", body: "It's a typo, fixing now.", createdAt: "2026-10-01T11:00:00Z" },
  ],
};

let addCommentMutate: ReturnType<typeof vi.fn>;

beforeEach(() => {
  useGetReportComments.mockReset();
  useAddReportComment.mockReset();
  addCommentMutate = vi.fn();
  useAddReportComment.mockReturnValue({ mutate: addCommentMutate, isPending: false, error: null });
});

describe("CommentsPanel", () => {
  it("shows a placeholder when there are no threads yet", () => {
    useGetReportComments.mockReturnValue({ isPending: false, data: [] });

    renderWithProviders(<CommentsPanel reportId="r1" fields={FIELDS} />);

    expect(screen.getByText("No comments yet.")).toBeInTheDocument();
  });

  it("renders an existing thread with its root comment and replies", () => {
    useGetReportComments.mockReturnValue({ isPending: false, data: [THREAD] });

    renderWithProviders(<CommentsPanel reportId="r1" fields={FIELDS} />);

    expect(screen.getByText("Tajnid")).toBeInTheDocument();
    expect(screen.getByText("Can you clarify this figure?")).toBeInTheDocument();
    expect(screen.getByText("Secretary Jane")).toBeInTheDocument();
    expect(screen.getByText("It's a typo, fixing now.")).toBeInTheDocument();
    expect(screen.getByText("Head Bello")).toBeInTheDocument();
  });

  it("starting a new thread requires both a field and a non-empty body", async () => {
    useGetReportComments.mockReturnValue({ isPending: false, data: [] });

    renderWithProviders(<CommentsPanel reportId="r1" fields={FIELDS} />);
    await userEvent.click(screen.getByRole("button", { name: /start a comment/i }));

    const postButton = screen.getByRole("button", { name: /^post$/i });
    expect(postButton).toBeDisabled();

    await userEvent.selectOptions(screen.getByLabelText("Field"), "tajnid");
    await userEvent.type(screen.getByPlaceholderText("Write a comment"), "Please double check this.");
    expect(postButton).toBeEnabled();

    await userEvent.click(postButton);
    expect(addCommentMutate).toHaveBeenCalledWith(
      { id: "r1", data: { fieldKey: "tajnid", parentId: null, body: "Please double check this." } },
      expect.anything(),
    );
  });

  it("replying to a thread posts with that thread's root id as the parent", async () => {
    useGetReportComments.mockReturnValue({ isPending: false, data: [THREAD] });

    renderWithProviders(<CommentsPanel reportId="r1" fields={FIELDS} />);
    await userEvent.type(screen.getByPlaceholderText("Write a reply"), "Confirmed, thanks.");
    await userEvent.click(screen.getByRole("button", { name: /reply/i }));

    expect(addCommentMutate).toHaveBeenCalledWith(
      { id: "r1", data: { fieldKey: null, parentId: "c1", body: "Confirmed, thanks." } },
      expect.anything(),
    );
  });
});
