"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { SaveIndicator, type SaveStatus } from "@/components/shared/SaveIndicator";
import type { ApiError } from "@/lib/api/client";
import type { TemplateSchema, UnitLevelSummary, UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import { getGetTemplateVersionsQueryKey, useUpdateTemplateSchema } from "@/lib/api/generated/templates/templates";
import { builderReducer, emptySchema } from "@/features/templates/utils/builder";
import { UNIT_LEVEL_CODE } from "@/features/templates/utils/report-type";
import { FieldPalette } from "./FieldPalette";
import { FieldPropertiesPanel } from "./FieldPropertiesPanel";
import { SectionCanvas } from "./SectionCanvas";

const AUTOSAVE_DELAY_MS = 1200;

export function BuildTab({
  reportTypeId,
  initialSchema,
  levels,
  units,
}: {
  reportTypeId: string;
  initialSchema: TemplateSchema;
  levels: UnitLevelSummary[];
  units: UnitTreeNode[];
}) {
  const queryClient = useQueryClient();
  const updateSchema = useUpdateTemplateSchema<ApiError>();

  const [schema, setSchema] = useState(initialSchema);
  const [activeSectionKey, setActiveSectionKey] = useState<string | null>(initialSchema.sections[0]?.key ?? null);
  const [selectedFieldKey, setSelectedFieldKey] = useState<string | null>(null);
  const [saveStatus, setSaveStatus] = useState<SaveStatus | null>(null);

  const dispatch = (action: Parameters<typeof builderReducer>[1]) => setSchema((current) => builderReducer(current, action));

  // Refs, not deps: the effect below should only restart its debounce timer when the schema
  // itself changes, never when updateSchema/queryClient are recreated by an unrelated render.
  const isFirstRender = useRef(true);
  const latest = useRef({ updateSchema, queryClient, reportTypeId });
  useEffect(() => {
    latest.current = { updateSchema, queryClient, reportTypeId };
  });

  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }

    setSaveStatus("saving");
    const timer = setTimeout(() => {
      const { updateSchema: mutate, queryClient: client, reportTypeId: id } = latest.current;
      mutate.mutate(
        { reportTypeId: id, data: { schema } },
        {
          onSuccess: () => {
            setSaveStatus("saved");
            client.invalidateQueries({ queryKey: getGetTemplateVersionsQueryKey(id) });
          },
          onError: () => setSaveStatus("error"),
        },
      );
    }, AUTOSAVE_DELAY_MS);

    return () => clearTimeout(timer);
  }, [schema]);

  const departments = units
    .filter((unit) => unit.levelCode === UNIT_LEVEL_CODE.Department && unit.archivedAt === null)
    .map((unit) => ({ id: unit.id, name: unit.name }));

  const selectedSection = schema.sections.find((section) => section.fields.some((field) => field.key === selectedFieldKey));
  const selectedField = selectedSection?.fields.find((field) => field.key === selectedFieldKey);

  const onAddFromPalette = (type: number) => {
    const targetSectionKey = activeSectionKey ?? schema.sections[0]?.key;
    if (!targetSectionKey) {
      return;
    }
    const next = builderReducer(schema, { type: "addField", sectionKey: targetSectionKey, fieldType: type });
    const newField = next.sections.find((section) => section.key === targetSectionKey)?.fields.at(-1);
    setSchema(next);
    setActiveSectionKey(targetSectionKey);
    if (newField) {
      setSelectedFieldKey(newField.key);
    }
  };

  return (
    <div className="grid gap-4 lg:grid-cols-[220px_1fr_340px]">
      <FieldPalette onAdd={onAddFromPalette} />

      <div className="flex flex-col gap-3">
        {saveStatus ? <SaveIndicator status={saveStatus} className="self-end" /> : null}
        <SectionCanvas
          schema={schema}
          activeSectionKey={activeSectionKey}
          selectedFieldKey={selectedFieldKey}
          onSelectSection={setActiveSectionKey}
          onSelectField={(sectionKey, fieldKey) => {
            setActiveSectionKey(sectionKey);
            setSelectedFieldKey(fieldKey);
          }}
          onUpdateSectionTitle={(sectionKey, title) => dispatch({ type: "updateSectionTitle", sectionKey, title })}
          onRemoveSection={(sectionKey) => {
            dispatch({ type: "removeSection", sectionKey });
            if (activeSectionKey === sectionKey) {
              setActiveSectionKey(null);
            }
          }}
          onAddSection={() => dispatch({ type: "addSection" })}
          onRemoveField={(sectionKey, fieldKey) => {
            dispatch({ type: "removeField", sectionKey, fieldKey });
            if (selectedFieldKey === fieldKey) {
              setSelectedFieldKey(null);
            }
          }}
          onDuplicateField={(sectionKey, fieldKey) => dispatch({ type: "duplicateField", sectionKey, fieldKey })}
          onReorderFields={(sectionKey, fromIndex, toIndex) => dispatch({ type: "reorderFields", sectionKey, fromIndex, toIndex })}
        />
      </div>

      {selectedSection && selectedField ? (
        <FieldPropertiesPanel
          reportTypeId={reportTypeId}
          field={selectedField}
          schema={schema}
          levels={levels}
          departments={departments}
          onUpdate={(patch) => dispatch({ type: "updateField", sectionKey: selectedSection.key, fieldKey: selectedField.key, patch })}
          onAddOption={() => dispatch({ type: "addOption", sectionKey: selectedSection.key, fieldKey: selectedField.key })}
          onRemoveOption={(optionValue) =>
            dispatch({ type: "removeOption", sectionKey: selectedSection.key, fieldKey: selectedField.key, optionValue })
          }
          onUpdateOption={(optionValue, patch) =>
            dispatch({ type: "updateOption", sectionKey: selectedSection.key, fieldKey: selectedField.key, optionValue, patch })
          }
          onAddColumn={() => dispatch({ type: "addColumn", sectionKey: selectedSection.key, fieldKey: selectedField.key })}
          onRemoveColumn={(columnKey) =>
            dispatch({ type: "removeColumn", sectionKey: selectedSection.key, fieldKey: selectedField.key, columnKey })
          }
          onUpdateColumn={(columnKey, patch) =>
            dispatch({ type: "updateColumn", sectionKey: selectedSection.key, fieldKey: selectedField.key, columnKey, patch })
          }
        />
      ) : null}
    </div>
  );
}

export { emptySchema };
