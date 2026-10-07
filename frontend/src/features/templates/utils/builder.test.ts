import { describe, expect, it } from "vitest";
import { builderReducer, createField, createSection, emptySchema } from "./builder";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";

describe("createField", () => {
  it("generates a key matching the backend's stable-key pattern", () => {
    const field = createField(TEMPLATE_FIELD_TYPE.ShortText, new Set());

    expect(field.key).toMatch(/^[a-z][a-z0-9_]*$/);
  });

  it("avoids a key collision with existing keys", () => {
    const taken = new Set(["field_abc123"]);
    const field = createField(TEMPLATE_FIELD_TYPE.ShortText, taken);

    expect(taken.has(field.key)).toBe(false);
  });

  it("seeds two default options for a choice field", () => {
    const field = createField(TEMPLATE_FIELD_TYPE.Dropdown, new Set());

    expect(field.options).toHaveLength(2);
  });

  it("seeds one default column for a repeatable table", () => {
    const field = createField(TEMPLATE_FIELD_TYPE.RepeatableTable, new Set());

    expect(field.columns).toHaveLength(1);
  });

  it("gives a computed field an empty expression to fill in", () => {
    const field = createField(TEMPLATE_FIELD_TYPE.Computed, new Set());

    expect(field.computedExpression).toBe("");
  });

  it("leaves options and columns null for a plain field", () => {
    const field = createField(TEMPLATE_FIELD_TYPE.ShortText, new Set());

    expect(field.options).toBeNull();
    expect(field.columns).toBeNull();
  });
});

describe("createSection", () => {
  it("generates a unique section key", () => {
    const section = createSection(new Set(["section_abc"]));

    expect(section.key).toMatch(/^[a-z][a-z0-9_]*$/);
    expect(section.key).not.toBe("section_abc");
    expect(section.fields).toEqual([]);
  });
});

describe("builderReducer", () => {
  it("addSection appends a new empty section", () => {
    const result = builderReducer(emptySchema(), { type: "addSection" });

    expect(result.sections).toHaveLength(1);
    expect(result.sections[0]!.fields).toEqual([]);
  });

  it("removeSection drops the matching section only", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    schema = builderReducer(schema, { type: "addSection" });
    const keepKey = schema.sections[1]!.key;

    const result = builderReducer(schema, { type: "removeSection", sectionKey: schema.sections[0]!.key });

    expect(result.sections).toHaveLength(1);
    expect(result.sections[0]!.key).toBe(keepKey);
  });

  it("updateSectionTitle changes only the targeted section's title", () => {
    const schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;

    const result = builderReducer(schema, { type: "updateSectionTitle", sectionKey, title: "Membership" });

    expect(result.sections[0]!.title).toBe("Membership");
  });

  it("addField appends a field of the requested type to the section", () => {
    const schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;

    const result = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.Number });

    expect(result.sections[0]!.fields).toHaveLength(1);
    expect(result.sections[0]!.fields[0]!.type).toBe(TEMPLATE_FIELD_TYPE.Number);
  });

  it("removeField drops the matching field only", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.ShortText });
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.Number });
    const keepKey = schema.sections[0]!.fields[1]!.key;

    const result = builderReducer(schema, { type: "removeField", sectionKey, fieldKey: schema.sections[0]!.fields[0]!.key });

    expect(result.sections[0]!.fields).toHaveLength(1);
    expect(result.sections[0]!.fields[0]!.key).toBe(keepKey);
  });

  it("duplicateField inserts a copy right after the original with a new key", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.ShortText });
    const originalKey = schema.sections[0]!.fields[0]!.key;

    const result = builderReducer(schema, { type: "duplicateField", sectionKey, fieldKey: originalKey });

    expect(result.sections[0]!.fields).toHaveLength(2);
    expect(result.sections[0]!.fields[0]!.key).toBe(originalKey);
    expect(result.sections[0]!.fields[1]!.key).not.toBe(originalKey);
    expect(result.sections[0]!.fields[1]!.label).toContain("copy");
  });

  it("updateField merges a patch into the matching field", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.ShortText });
    const fieldKey = schema.sections[0]!.fields[0]!.key;

    const result = builderReducer(schema, { type: "updateField", sectionKey, fieldKey, patch: { label: "Full name", required: true } });

    expect(result.sections[0]!.fields[0]!.label).toBe("Full name");
    expect(result.sections[0]!.fields[0]!.required).toBe(true);
  });

  it("reorderFields moves a field to a new index", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.ShortText });
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.Number });
    const [first, second] = schema.sections[0]!.fields;

    const result = builderReducer(schema, { type: "reorderFields", sectionKey, fromIndex: 0, toIndex: 1 });

    expect(result.sections[0]!.fields[0]!.key).toBe(second!.key);
    expect(result.sections[0]!.fields[1]!.key).toBe(first!.key);
  });

  it("addOption appends a new option to a choice field", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.Dropdown });
    const fieldKey = schema.sections[0]!.fields[0]!.key;

    const result = builderReducer(schema, { type: "addOption", sectionKey, fieldKey });

    expect(result.sections[0]!.fields[0]!.options).toHaveLength(3);
  });

  it("removeOption drops only the matching option", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.Dropdown });
    const fieldKey = schema.sections[0]!.fields[0]!.key;
    const firstValue = schema.sections[0]!.fields[0]!.options![0]!.value;

    const result = builderReducer(schema, { type: "removeOption", sectionKey, fieldKey, optionValue: firstValue });

    expect(result.sections[0]!.fields[0]!.options).toHaveLength(1);
    expect(result.sections[0]!.fields[0]!.options![0]!.value).not.toBe(firstValue);
  });

  it("addColumn appends a column to a repeatable table field", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.RepeatableTable });
    const fieldKey = schema.sections[0]!.fields[0]!.key;

    const result = builderReducer(schema, { type: "addColumn", sectionKey, fieldKey });

    expect(result.sections[0]!.fields[0]!.columns).toHaveLength(2);
  });

  it("removeColumn drops only the matching column", () => {
    let schema = builderReducer(emptySchema(), { type: "addSection" });
    const sectionKey = schema.sections[0]!.key;
    schema = builderReducer(schema, { type: "addField", sectionKey, fieldType: TEMPLATE_FIELD_TYPE.RepeatableTable });
    const fieldKey = schema.sections[0]!.fields[0]!.key;
    const firstColumnKey = schema.sections[0]!.fields[0]!.columns![0]!.key;
    schema = builderReducer(schema, { type: "addColumn", sectionKey, fieldKey });

    const result = builderReducer(schema, { type: "removeColumn", sectionKey, fieldKey, columnKey: firstColumnKey });

    expect(result.sections[0]!.fields[0]!.columns).toHaveLength(1);
    expect(result.sections[0]!.fields[0]!.columns![0]!.key).not.toBe(firstColumnKey);
  });
});
