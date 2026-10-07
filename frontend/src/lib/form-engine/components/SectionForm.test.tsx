import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { useForm } from "react-hook-form";
import { describe, expect, it } from "vitest";
import messages from "../../../../messages/en.json";
import type { TemplateField, TemplateSection } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "../field-types";
import { SectionForm } from "./SectionForm";

function field(overrides: Partial<TemplateField>): TemplateField {
  return {
    key: "field_a",
    type: TEMPLATE_FIELD_TYPE.ShortText,
    label: "Field A",
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
    ...overrides,
  };
}

function Harness({ section, unitLevel = 3 }: { section: TemplateSection; unitLevel?: number }) {
  const form = useForm<Record<string, unknown>>();
  return <SectionForm section={section} unitLevel={unitLevel} form={form} />;
}

function renderSection(fields: TemplateField[], unitLevel = 3) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <Harness section={{ key: "s1", title: "Section 1", fields }} unitLevel={unitLevel} />
    </NextIntlClientProvider>,
  );
}

describe("SectionForm", () => {
  it("renders a short text field", () => {
    renderSection([field({ key: "name", label: "Name", type: TEMPLATE_FIELD_TYPE.ShortText })]);

    expect(screen.getByLabelText("Name")).toBeInTheDocument();
  });

  it("renders a dropdown with its options", () => {
    renderSection([
      field({
        key: "color",
        label: "Favorite color",
        type: TEMPLATE_FIELD_TYPE.Dropdown,
        options: [
          { value: "red", label: "Red" },
          { value: "blue", label: "Blue" },
        ],
      }),
    ]);

    expect(screen.getByRole("option", { name: "Red" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Blue" })).toBeInTheDocument();
  });

  it("hides a field whose level doesn't match the current unit", () => {
    renderSection([field({ key: "muqam_only", label: "Muqam only", visibleForLevels: [3] })], 1);

    expect(screen.queryByLabelText("Muqam only")).not.toBeInTheDocument();
  });

  it("shows a field reactively once its showIf trigger answer matches", async () => {
    const user = userEvent.setup();
    renderSection([
      field({ key: "held_meeting", label: "Held meeting?", type: TEMPLATE_FIELD_TYPE.YesNo }),
      field({
        key: "attendance",
        label: "Attendance",
        type: TEMPLATE_FIELD_TYPE.Number,
        showIf: { fieldKey: "held_meeting", equalsValue: "true" },
      }),
    ]);

    expect(screen.queryByLabelText("Attendance")).not.toBeInTheDocument();

    await user.click(screen.getByRole("radio", { name: "Yes" }));

    expect(screen.getByLabelText("Attendance")).toBeInTheDocument();
  });

  it("recomputes a computed field live as its dependencies change", async () => {
    const user = userEvent.setup();
    renderSection([
      field({ key: "a", label: "A", type: TEMPLATE_FIELD_TYPE.Number }),
      field({ key: "b", label: "B", type: TEMPLATE_FIELD_TYPE.Number }),
      field({ key: "total", label: "Total", type: TEMPLATE_FIELD_TYPE.Computed, computedExpression: "sum(a, b)" }),
    ]);

    expect(screen.getByText("0")).toBeInTheDocument();

    await user.type(screen.getByLabelText("A"), "3");
    await user.type(screen.getByLabelText("B"), "4");

    expect(await screen.findByText("7")).toBeInTheDocument();
  });

  it("adds and removes rows in a repeatable table", async () => {
    const user = userEvent.setup();
    renderSection([
      field({
        key: "activities",
        label: "Activities",
        type: TEMPLATE_FIELD_TYPE.RepeatableTable,
        columns: [field({ key: "activity_name", label: "Activity", type: TEMPLATE_FIELD_TYPE.ShortText })],
      }),
    ]);

    expect(screen.getByText(/no rows yet/i)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /add row/i }));
    expect(screen.queryByText(/no rows yet/i)).not.toBeInTheDocument();
    expect(screen.getByRole("textbox")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /remove row 1/i }));
    expect(screen.getByText(/no rows yet/i)).toBeInTheDocument();
  });

  it("adds and removes entries in a member list", async () => {
    const user = userEvent.setup();
    renderSection([field({ key: "members", label: "Members", type: TEMPLATE_FIELD_TYPE.MemberList })]);

    expect(screen.getByText(/no members yet/i)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /add member/i }));
    expect(screen.getByLabelText("Member 1")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /remove member 1/i }));
    expect(screen.getByText(/no members yet/i)).toBeInTheDocument();
  });
});
