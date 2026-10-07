import { closestCenter, DndContext, type DragEndEvent, PointerSensor, useSensor, useSensors } from "@dnd-kit/core";
import { SortableContext, useSortable, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { Copy, GripVertical, Plus, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { TemplateField, TemplateSchema, TemplateSection } from "@/lib/api/generated/hRSApiV1.schemas";
import { cn } from "@/lib/utils";
import { fieldTypeLabel } from "@/features/templates/utils/field-type-meta";

export function SectionCanvas({
  schema,
  activeSectionKey,
  selectedFieldKey,
  onSelectSection,
  onSelectField,
  onUpdateSectionTitle,
  onRemoveSection,
  onAddSection,
  onRemoveField,
  onDuplicateField,
  onReorderFields,
}: {
  schema: TemplateSchema;
  activeSectionKey: string | null;
  selectedFieldKey: string | null;
  onSelectSection: (sectionKey: string) => void;
  onSelectField: (sectionKey: string, fieldKey: string) => void;
  onUpdateSectionTitle: (sectionKey: string, title: string) => void;
  onRemoveSection: (sectionKey: string) => void;
  onAddSection: () => void;
  onRemoveField: (sectionKey: string, fieldKey: string) => void;
  onDuplicateField: (sectionKey: string, fieldKey: string) => void;
  onReorderFields: (sectionKey: string, fromIndex: number, toIndex: number) => void;
}) {
  const t = useTranslations("TemplateBuilder");

  return (
    <div className="flex flex-col gap-4">
      {schema.sections.map((section) => (
        <div
          key={section.key}
          className={cn(
            "rounded-lg border bg-surface p-4",
            activeSectionKey === section.key ? "border-green-600 ring-1 ring-green-600/30" : "border-line",
          )}
          onClick={() => onSelectSection(section.key)}
        >
          <div className="mb-3 flex items-center justify-between gap-2">
            <Input
              value={section.title}
              onChange={(event) => onUpdateSectionTitle(section.key, event.target.value)}
              onClick={(event) => event.stopPropagation()}
              className="max-w-xs font-semibold"
              aria-label={t("sectionTitleLabel")}
            />
            <Button
              type="button"
              variant="ghost"
              size="sm"
              aria-label={t("removeSection", { title: section.title })}
              onClick={(event) => {
                event.stopPropagation();
                onRemoveSection(section.key);
              }}
            >
              <Trash2 className="size-4" aria-hidden="true" />
            </Button>
          </div>

          <SortableFieldList
            section={section}
            selectedFieldKey={selectedFieldKey}
            onSelectField={(fieldKey) => onSelectField(section.key, fieldKey)}
            onRemoveField={(fieldKey) => onRemoveField(section.key, fieldKey)}
            onDuplicateField={(fieldKey) => onDuplicateField(section.key, fieldKey)}
            onReorder={(fromIndex, toIndex) => onReorderFields(section.key, fromIndex, toIndex)}
          />
        </div>
      ))}

      <Button type="button" variant="outline" className="self-start" onClick={onAddSection}>
        <Plus aria-hidden="true" />
        {t("addSection")}
      </Button>
    </div>
  );
}

function SortableFieldList({
  section,
  selectedFieldKey,
  onSelectField,
  onRemoveField,
  onDuplicateField,
  onReorder,
}: {
  section: TemplateSection;
  selectedFieldKey: string | null;
  onSelectField: (fieldKey: string) => void;
  onRemoveField: (fieldKey: string) => void;
  onDuplicateField: (fieldKey: string) => void;
  onReorder: (fromIndex: number, toIndex: number) => void;
}) {
  const t = useTranslations("TemplateBuilder");
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (!over || active.id === over.id) {
      return;
    }
    const fromIndex = section.fields.findIndex((field) => field.key === active.id);
    const toIndex = section.fields.findIndex((field) => field.key === over.id);
    if (fromIndex !== -1 && toIndex !== -1) {
      onReorder(fromIndex, toIndex);
    }
  };

  if (section.fields.length === 0) {
    return <p className="rounded-md border border-dashed border-line-strong p-4 text-center text-sm text-ink-muted">{t("noFieldsYet")}</p>;
  }

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
      <SortableContext items={section.fields.map((field) => field.key)} strategy={verticalListSortingStrategy}>
        <div className="flex flex-col gap-2">
          {section.fields.map((field) => (
            <SortableFieldRow
              key={field.key}
              field={field}
              selected={selectedFieldKey === field.key}
              onSelect={() => onSelectField(field.key)}
              onRemove={() => onRemoveField(field.key)}
              onDuplicate={() => onDuplicateField(field.key)}
            />
          ))}
        </div>
      </SortableContext>
    </DndContext>
  );
}

function SortableFieldRow({
  field,
  selected,
  onSelect,
  onRemove,
  onDuplicate,
}: {
  field: TemplateField;
  selected: boolean;
  onSelect: () => void;
  onRemove: () => void;
  onDuplicate: () => void;
}) {
  const t = useTranslations("TemplateBuilder");
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: field.key });

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn(
        "flex items-center gap-2 rounded-md border bg-surface p-2",
        selected ? "border-green-600 bg-mint" : "border-line",
        isDragging && "opacity-50",
      )}
    >
      <button type="button" aria-label={t("dragField", { label: field.label })} className="cursor-grab text-ink-muted" {...attributes} {...listeners}>
        <GripVertical className="size-4" aria-hidden="true" />
      </button>

      <button type="button" onClick={onSelect} className="flex flex-1 flex-col items-start gap-0.5 text-left">
        <span className="text-sm font-medium text-ink">{field.label}</span>
        <span className="flex flex-wrap gap-1 text-xs text-ink-muted">
          <span className="rounded-full bg-ground px-1.5 py-0.5">{fieldTypeLabel(field.type)}</span>
          {field.required ? <span className="rounded-full bg-status-returned-bg px-1.5 py-0.5 text-status-returned-fg">{t("requiredBadge")}</span> : null}
          {field.showIf ? <span className="rounded-full bg-status-upcoming-bg px-1.5 py-0.5 text-status-upcoming-fg">{t("conditionalBadge")}</span> : null}
        </span>
      </button>

      <Button type="button" variant="ghost" size="sm" aria-label={t("duplicateField", { label: field.label })} onClick={onDuplicate}>
        <Copy className="size-4" aria-hidden="true" />
      </Button>
      <Button type="button" variant="ghost" size="sm" aria-label={t("removeField", { label: field.label })} onClick={onRemove}>
        <Trash2 className="size-4" aria-hidden="true" />
      </Button>
    </div>
  );
}
