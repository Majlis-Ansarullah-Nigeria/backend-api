import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { BULK_IMPORT_OUTCOME } from "@/features/organization/utils/bulk-import";
import { BulkImportWizard } from "./BulkImportWizard";

const previewMutate = vi.fn();
const commitMutate = vi.fn();
const getBulkImportTemplate = vi.fn();
const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("@/lib/api/generated/organization/organization", () => ({
  usePreviewBulkImport: () => ({ mutate: previewMutate, isPending: false, isError: false, error: null, reset: vi.fn() }),
  useCommitBulkImport: () => ({ mutate: commitMutate, isPending: false, isError: false }),
  getBulkImportTemplate: (...args: unknown[]) => getBulkImportTemplate(...args),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const SUMMARY = {
  totalRows: 2,
  newCount: 1,
  updateCount: 0,
  warningCount: 0,
  errorCount: 1,
  rows: [
    { rowNumber: 1, outcome: BULK_IMPORT_OUTCOME.New, level: "Zone", code: "Z01", name: "Zone 1", parentCode: "", headEmail: "", issue: null },
    { rowNumber: 2, outcome: BULK_IMPORT_OUTCOME.Error, level: "Muqam", code: "ZZ", name: "Ghost", parentCode: "ZZ", headEmail: "", issue: "Parent code not found." },
  ],
};

beforeEach(() => {
  previewMutate.mockReset();
  commitMutate.mockReset();
  getBulkImportTemplate.mockReset();
  push.mockReset();
});

async function selectFile() {
  const user = userEvent.setup();
  const file = new File(["Level,Code,Name,Parent Code"], "units.csv", { type: "text/csv" });
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  await user.upload(input, file);
  return user;
}

describe("BulkImportWizard", () => {
  it("starts on the upload step", () => {
    renderWithIntl(<BulkImportWizard />);

    expect(screen.getByText(/upload a spreadsheet/i)).toBeInTheDocument();
  });

  it("previews the selected file and advances to the columns step on success", async () => {
    renderWithIntl(<BulkImportWizard />);
    await selectFile();

    expect(previewMutate).toHaveBeenCalledWith(
      { data: { file: expect.any(File) } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );

    // Simulate the mutation succeeding.
    const onSuccess = previewMutate.mock.calls[0][1].onSuccess;
    onSuccess(SUMMARY);

    await waitFor(() => expect(screen.getByText(/columns matched/i)).toBeInTheDocument());
  });

  it("shows the row summary and import button after continuing past the columns step", async () => {
    renderWithIntl(<BulkImportWizard />);
    await selectFile();
    previewMutate.mock.calls[0][1].onSuccess(SUMMARY);
    const user = userEvent.setup();

    await waitFor(() => screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /continue/i }));

    expect(screen.getByRole("button", { name: /import 1 rows, skip 1 errors/i })).toBeInTheDocument();
  });

  it("commits the same file when Import is clicked", async () => {
    renderWithIntl(<BulkImportWizard />);
    await selectFile();
    previewMutate.mock.calls[0][1].onSuccess(SUMMARY);
    const user = userEvent.setup();

    await waitFor(() => screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /import 1 rows/i }));

    expect(commitMutate).toHaveBeenCalledWith(
      { data: { file: expect.any(File) } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it("shows the done step and navigates to the organization screen", async () => {
    renderWithIntl(<BulkImportWizard />);
    await selectFile();
    previewMutate.mock.calls[0][1].onSuccess(SUMMARY);
    const user = userEvent.setup();

    await waitFor(() => screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /import 1 rows/i }));
    commitMutate.mock.calls[0][1].onSuccess({ createdCount: 1, updatedCount: 0, skippedErrorCount: 1, invitedCount: 0 });

    await waitFor(() => expect(screen.getByText(/import complete/i)).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: /view organization/i }));
    expect(push).toHaveBeenCalledWith("/secretariat/organization");
  });

  it("downloads the template when the button is clicked", async () => {
    getBulkImportTemplate.mockResolvedValue(new Blob(["x"]));
    const originalCreateObjectURL = URL.createObjectURL;
    const originalRevokeObjectURL = URL.revokeObjectURL;
    URL.createObjectURL = vi.fn(() => "blob:mock");
    URL.revokeObjectURL = vi.fn();

    renderWithIntl(<BulkImportWizard />);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: /download template/i }));

    await waitFor(() => expect(getBulkImportTemplate).toHaveBeenCalled());

    URL.createObjectURL = originalCreateObjectURL;
    URL.revokeObjectURL = originalRevokeObjectURL;
  });
});
