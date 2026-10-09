import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { useForm } from "react-hook-form";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "../field-types";
import { FileUploadField } from "./FileUploadField";

const presignAttachmentUpload = vi.fn();
const completeAttachmentUpload = vi.fn();
const deleteAttachment = vi.fn();

vi.mock("@/lib/api/generated/submissions/submissions", () => ({
  presignAttachmentUpload: (...args: unknown[]) => presignAttachmentUpload(...args),
  completeAttachmentUpload: (...args: unknown[]) => completeAttachmentUpload(...args),
  deleteAttachment: (...args: unknown[]) => deleteAttachment(...args),
}));

const PHOTOS_FIELD: TemplateField = {
  key: "photos",
  type: TEMPLATE_FIELD_TYPE.FileUpload,
  label: "Photos",
  helpText: null,
  required: false,
  min: null,
  max: null,
  regex: null,
  defaultValue: null,
  unitLabel: null,
  confidential: false,
  owningDepartmentId: null,
  visibleForLevels: null,
  showIf: null,
  aggregation: 0,
  computedExpression: null,
  options: null,
  columns: null,
};

function Harness({ uploadContext, defaultAttachments }: { uploadContext?: { reportId: string; unitId: string }; defaultAttachments?: unknown[] }) {
  const form = useForm<Record<string, unknown>>({ defaultValues: { photos: defaultAttachments ?? [] } });
  return <FileUploadField field={PHOTOS_FIELD} form={form} uploadContext={uploadContext} />;
}

function renderField(props: { uploadContext?: { reportId: string; unitId: string }; defaultAttachments?: unknown[] } = {}) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <Harness {...props} />
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  presignAttachmentUpload.mockReset();
  completeAttachmentUpload.mockReset();
  deleteAttachment.mockReset();
  vi.stubGlobal(
    "fetch",
    vi.fn(() => Promise.resolve(new Response(null, { status: 200 }))),
  );
});

describe("FileUploadField", () => {
  it("renders a disabled preview-only stub with no upload context (the template builder's preview)", () => {
    renderField({ uploadContext: undefined });

    expect(screen.getByText(/preview only/i)).toBeInTheDocument();
    expect(screen.queryByText(/add files/i)).not.toBeInTheDocument();
  });

  it("uploads a selected file through presign, PUT and complete, then shows it in the list", async () => {
    const user = userEvent.setup();
    presignAttachmentUpload.mockResolvedValue({ attachmentId: "att-1", uploadUrl: "https://fake-storage.test/key-1" });
    completeAttachmentUpload.mockResolvedValue({
      id: "att-1",
      fieldKey: "photos",
      fileName: "site.jpg",
      contentType: "image/jpeg",
      sizeBytes: 1234,
    });

    renderField({ uploadContext: { reportId: "report-1", unitId: "unit-1" } });

    const file = new File(["hello"], "site.jpg", { type: "image/jpeg" });
    const input = screen.getByLabelText(/add files/i);
    await user.upload(input, file);

    expect(await screen.findByText("site.jpg")).toBeInTheDocument();
    expect(presignAttachmentUpload).toHaveBeenCalledWith(
      "report-1",
      expect.objectContaining({ fieldKey: "photos", fileName: "site.jpg" }),
      { unit: "unit-1" },
    );
    expect(completeAttachmentUpload).toHaveBeenCalledWith("report-1", "att-1", { unit: "unit-1" });
  });

  it("shows an inline, dismissible error when the upload fails", async () => {
    const user = userEvent.setup();
    presignAttachmentUpload.mockRejectedValue(new Error("nope"));

    renderField({ uploadContext: { reportId: "report-1", unitId: "unit-1" } });

    const file = new File(["hello"], "site.jpg", { type: "image/jpeg" });
    await user.upload(screen.getByLabelText(/add files/i), file);

    await waitFor(() => expect(screen.getByRole("button", { name: /dismiss/i })).toBeInTheDocument());

    await user.click(screen.getByRole("button", { name: /dismiss/i }));
    expect(screen.queryByRole("button", { name: /dismiss/i })).not.toBeInTheDocument();
  });

  it("removes an already-uploaded attachment and calls deleteAttachment", async () => {
    const user = userEvent.setup();
    deleteAttachment.mockResolvedValue(undefined);

    renderField({
      uploadContext: { reportId: "report-1", unitId: "unit-1" },
      defaultAttachments: [{ id: "att-1", fieldKey: "photos", fileName: "site.jpg", contentType: "image/jpeg", sizeBytes: 1234 }],
    });

    expect(screen.getByText("site.jpg")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /remove site\.jpg/i }));

    expect(screen.queryByText("site.jpg")).not.toBeInTheDocument();
    expect(deleteAttachment).toHaveBeenCalledWith("report-1", "att-1", { unit: "unit-1" });
  });
});
