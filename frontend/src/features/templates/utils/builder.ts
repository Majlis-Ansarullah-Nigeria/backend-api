import type { TemplateField, TemplateFieldOption, TemplateSchema, TemplateSection } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";

const KEY_CHARS = "abcdefghijklmnopqrstuvwxyz0123456789";

function randomSuffix(length = 6): string {
  let result = "";
  for (let i = 0; i < length; i++) {
    result += KEY_CHARS[Math.floor(Math.random() * KEY_CHARS.length)];
  }
  return result;
}

function allFieldKeys(schema: TemplateSchema): Set<string> {
  return new Set(schema.sections.flatMap((section) => section.fields.map((field) => field.key)));
}

function allSectionKeys(schema: TemplateSchema): Set<string> {
  return new Set(schema.sections.map((section) => section.key));
}

/** Prefixed with a letter so the result always satisfies the backend's stable-key pattern
 * (^[a-z][a-z0-9_]*$) regardless of what the random suffix happens to contain. */
function uniqueKey(prefix: string, taken: Set<string>): string {
  let key = `${prefix}_${randomSuffix()}`;
  while (taken.has(key)) {
    key = `${prefix}_${randomSuffix()}`;
  }
  return key;
}

const CHOICE_TYPES: readonly number[] = [
  TEMPLATE_FIELD_TYPE.SingleChoice,
  TEMPLATE_FIELD_TYPE.MultipleChoice,
  TEMPLATE_FIELD_TYPE.Dropdown,
];

export function createField(type: number, existingKeys: Set<string>): TemplateField {
  const key = uniqueKey("field", existingKeys);
  return {
    key,
    type,
    label: "New field",
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
    computedExpression: type === TEMPLATE_FIELD_TYPE.Computed ? "" : null,
    options: CHOICE_TYPES.includes(type)
      ? [
          { value: "option_1", label: "Option 1" },
          { value: "option_2", label: "Option 2" },
        ]
      : null,
    columns:
      type === TEMPLATE_FIELD_TYPE.RepeatableTable
        ? [
            {
              key: uniqueKey("column", new Set()),
              type: TEMPLATE_FIELD_TYPE.ShortText,
              label: "Column 1",
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
            },
          ]
        : null,
    placeholder: null,
  };
}

export function createSection(existingKeys: Set<string>): TemplateSection {
  return { key: uniqueKey("section", existingKeys), title: "New section", fields: [] };
}

export function emptySchema(): TemplateSchema {
  return { sections: [] };
}

export type BuilderAction =
  | { type: "replaceSchema"; schema: TemplateSchema }
  | { type: "addSection" }
  | { type: "removeSection"; sectionKey: string }
  | { type: "updateSectionTitle"; sectionKey: string; title: string }
  | { type: "addField"; sectionKey: string; fieldType: number }
  | { type: "removeField"; sectionKey: string; fieldKey: string }
  | { type: "duplicateField"; sectionKey: string; fieldKey: string }
  | { type: "updateField"; sectionKey: string; fieldKey: string; patch: Partial<TemplateField> }
  | { type: "reorderFields"; sectionKey: string; fromIndex: number; toIndex: number }
  | { type: "addOption"; sectionKey: string; fieldKey: string }
  | { type: "removeOption"; sectionKey: string; fieldKey: string; optionValue: string }
  | { type: "updateOption"; sectionKey: string; fieldKey: string; optionValue: string; patch: Partial<TemplateFieldOption> }
  | { type: "addColumn"; sectionKey: string; fieldKey: string }
  | { type: "removeColumn"; sectionKey: string; fieldKey: string; columnKey: string }
  | { type: "updateColumn"; sectionKey: string; fieldKey: string; columnKey: string; patch: Partial<TemplateField> };

function mapSection(schema: TemplateSchema, sectionKey: string, update: (section: TemplateSection) => TemplateSection): TemplateSchema {
  return {
    sections: schema.sections.map((section) => (section.key === sectionKey ? update(section) : section)),
  };
}

function mapField(section: TemplateSection, fieldKey: string, update: (field: TemplateField) => TemplateField): TemplateSection {
  return { ...section, fields: section.fields.map((field) => (field.key === fieldKey ? update(field) : field)) };
}

export function builderReducer(schema: TemplateSchema, action: BuilderAction): TemplateSchema {
  switch (action.type) {
    case "replaceSchema":
      return action.schema;

    case "addSection":
      return { sections: [...schema.sections, createSection(allSectionKeys(schema))] };

    case "removeSection":
      return { sections: schema.sections.filter((section) => section.key !== action.sectionKey) };

    case "updateSectionTitle":
      return mapSection(schema, action.sectionKey, (section) => ({ ...section, title: action.title }));

    case "addField":
      return mapSection(schema, action.sectionKey, (section) => ({
        ...section,
        fields: [...section.fields, createField(action.fieldType, allFieldKeys(schema))],
      }));

    case "removeField":
      return mapSection(schema, action.sectionKey, (section) => ({
        ...section,
        fields: section.fields.filter((field) => field.key !== action.fieldKey),
      }));

    case "duplicateField":
      return mapSection(schema, action.sectionKey, (section) => {
        const source = section.fields.find((field) => field.key === action.fieldKey);
        if (!source) {
          return section;
        }
        const copy: TemplateField = { ...source, key: uniqueKey("field", allFieldKeys(schema)), label: `${source.label} (copy)` };
        const index = section.fields.findIndex((field) => field.key === action.fieldKey);
        return { ...section, fields: [...section.fields.slice(0, index + 1), copy, ...section.fields.slice(index + 1)] };
      });

    case "updateField":
      return mapSection(schema, action.sectionKey, (section) =>
        mapField(section, action.fieldKey, (field) => ({ ...field, ...action.patch })),
      );

    case "reorderFields":
      return mapSection(schema, action.sectionKey, (section) => {
        const fields = [...section.fields];
        const [moved] = fields.splice(action.fromIndex, 1);
        if (!moved) {
          return section;
        }
        fields.splice(action.toIndex, 0, moved);
        return { ...section, fields };
      });

    case "addOption":
      return mapSection(schema, action.sectionKey, (section) =>
        mapField(section, action.fieldKey, (field) => {
          const options = field.options ?? [];
          const existing = new Set(options.map((option) => option.value));
          const value = uniqueKey("option", existing);
          return { ...field, options: [...options, { value, label: `Option ${options.length + 1}` }] };
        }),
      );

    case "removeOption":
      return mapSection(schema, action.sectionKey, (section) =>
        mapField(section, action.fieldKey, (field) => ({
          ...field,
          options: (field.options ?? []).filter((option) => option.value !== action.optionValue),
        })),
      );

    case "updateOption":
      return mapSection(schema, action.sectionKey, (section) =>
        mapField(section, action.fieldKey, (field) => ({
          ...field,
          options: (field.options ?? []).map((option) =>
            option.value === action.optionValue ? { ...option, ...action.patch } : option,
          ),
        })),
      );

    case "addColumn":
      return mapSection(schema, action.sectionKey, (section) =>
        mapField(section, action.fieldKey, (field) => {
          const columns = field.columns ?? [];
          const existing = new Set(columns.map((column) => column.key));
          return {
            ...field,
            columns: [...columns, createField(TEMPLATE_FIELD_TYPE.ShortText, existing)].map((column, index) =>
              column.label === "New field" ? { ...column, label: `Column ${index + 1}` } : column,
            ),
          };
        }),
      );

    case "removeColumn":
      return mapSection(schema, action.sectionKey, (section) =>
        mapField(section, action.fieldKey, (field) => ({
          ...field,
          columns: (field.columns ?? []).filter((column) => column.key !== action.columnKey),
        })),
      );

    case "updateColumn":
      return mapSection(schema, action.sectionKey, (section) =>
        mapField(section, action.fieldKey, (field) => ({
          ...field,
          columns: (field.columns ?? []).map((column) => (column.key === action.columnKey ? { ...column, ...action.patch } : column)),
        })),
      );

    default:
      return schema;
  }
}
