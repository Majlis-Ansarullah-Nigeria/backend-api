"use client";

import { Plus, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { evaluateExpression } from "@/lib/form-engine/expression-evaluator";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";
import type { TemplateField, TemplateFieldOption, TemplateSchema, UnitLevelSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { aggregationLabel, NUMERIC_AGGREGATABLE_TYPES } from "@/features/templates/utils/field-type-meta";
import { AGGREGATION_RULE } from "@/lib/form-engine/field-types";
import { cn } from "@/lib/utils";
import { FieldTargetsTab } from "./FieldTargetsTab";

type PropertiesTab = "field" | "logic" | "visibility" | "targets";

const CHOICE_TYPES: readonly number[] = [TEMPLATE_FIELD_TYPE.SingleChoice, TEMPLATE_FIELD_TYPE.MultipleChoice, TEMPLATE_FIELD_TYPE.Dropdown];
// Figures dashboards chart (FR-DSH-002) — the only fields that can have targets (FR-TPL-011).
const FIGURE_TYPES: readonly number[] = [TEMPLATE_FIELD_TYPE.Number, TEMPLATE_FIELD_TYPE.Currency, TEMPLATE_FIELD_TYPE.Percentage];

export function FieldPropertiesPanel({
  reportTypeId,
  field,
  schema,
  levels,
  departments,
  onUpdate,
  onAddOption,
  onRemoveOption,
  onUpdateOption,
  onAddColumn,
  onRemoveColumn,
  onUpdateColumn,
}: {
  reportTypeId?: string;
  field: TemplateField;
  schema: TemplateSchema;
  levels: UnitLevelSummary[];
  departments: { id: string; name: string }[];
  onUpdate: (patch: Partial<TemplateField>) => void;
  onAddOption: () => void;
  onRemoveOption: (optionValue: string) => void;
  onUpdateOption: (optionValue: string, patch: Partial<TemplateFieldOption>) => void;
  onAddColumn: () => void;
  onRemoveColumn: (columnKey: string) => void;
  onUpdateColumn: (columnKey: string, patch: Partial<TemplateField>) => void;
}) {
  const t = useTranslations("TemplateBuilder");
  const [tab, setTab] = useState<PropertiesTab>("field");
  const hasTargets = reportTypeId != null && FIGURE_TYPES.includes(Number(field.type));

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-4">
      <div className="flex gap-1 border-b border-line" role="tablist" aria-label={t("propertiesTabsLabel")}>
        <PanelTab active={tab === "field"} onClick={() => setTab("field")} label={t("fieldTab")} />
        <PanelTab active={tab === "logic"} onClick={() => setTab("logic")} label={t("logicTab")} />
        <PanelTab active={tab === "visibility"} onClick={() => setTab("visibility")} label={t("visibilityTab")} />
        {hasTargets ? <PanelTab active={tab === "targets"} onClick={() => setTab("targets")} label={t("targetsTab")} /> : null}
      </div>

      {tab === "field" ? (
        <FieldTab
          field={field}
          departments={departments}
          onUpdate={onUpdate}
          onAddOption={onAddOption}
          onRemoveOption={onRemoveOption}
          onUpdateOption={onUpdateOption}
          onAddColumn={onAddColumn}
          onRemoveColumn={onRemoveColumn}
          onUpdateColumn={onUpdateColumn}
        />
      ) : tab === "logic" ? (
        <LogicTab field={field} schema={schema} onUpdate={onUpdate} />
      ) : tab === "targets" && hasTargets && reportTypeId ? (
        <FieldTargetsTab reportTypeId={reportTypeId} fieldKey={field.key} levels={levels} />
      ) : (
        <VisibilityTab field={field} levels={levels} onUpdate={onUpdate} />
      )}
    </div>
  );
}

function PanelTab({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "border-b-2 px-3 py-2 text-sm font-medium",
        active ? "border-green-600 text-green-700" : "border-transparent text-ink-muted hover:text-ink",
      )}
    >
      {label}
    </button>
  );
}

function FieldTab({
  field,
  departments,
  onUpdate,
  onAddOption,
  onRemoveOption,
  onUpdateOption,
  onAddColumn,
  onRemoveColumn,
  onUpdateColumn,
}: {
  field: TemplateField;
  departments: { id: string; name: string }[];
  onUpdate: (patch: Partial<TemplateField>) => void;
  onAddOption: () => void;
  onRemoveOption: (optionValue: string) => void;
  onUpdateOption: (optionValue: string, patch: Partial<TemplateFieldOption>) => void;
  onAddColumn: () => void;
  onRemoveColumn: (columnKey: string) => void;
  onUpdateColumn: (columnKey: string, patch: Partial<TemplateField>) => void;
}) {
  const t = useTranslations("TemplateBuilder");
  const isNumeric = NUMERIC_AGGREGATABLE_TYPES.includes(field.type);
  const isChoice = CHOICE_TYPES.includes(field.type);
  const isTable = field.type === TEMPLATE_FIELD_TYPE.RepeatableTable;
  const isComputed = field.type === TEMPLATE_FIELD_TYPE.Computed;

  const expressionCheck = isComputed && field.computedExpression ? evaluateExpression(field.computedExpression, {}) : null;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="prop-label">{t("labelLabel")}</Label>
        <Input id="prop-label" value={field.label} onChange={(event) => onUpdate({ label: event.target.value })} />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="prop-help">{t("helpTextLabel")}</Label>
        <Input id="prop-help" value={field.helpText ?? ""} onChange={(event) => onUpdate({ helpText: event.target.value || null })} />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="prop-placeholder">{t("placeholderLabel")}</Label>
        <Input
          id="prop-placeholder"
          value={field.placeholder ?? ""}
          onChange={(event) => onUpdate({ placeholder: event.target.value || null })}
        />
      </div>

      {isComputed ? (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="prop-expression">{t("expressionLabel")}</Label>
          <Input
            id="prop-expression"
            value={field.computedExpression ?? ""}
            onChange={(event) => onUpdate({ computedExpression: event.target.value })}
            placeholder="sum(a, b)"
          />
          {field.computedExpression && expressionCheck && !expressionCheck.success ? (
            <p className="text-xs text-danger">{expressionCheck.error}</p>
          ) : null}
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="prop-min">{t("minLabel")}</Label>
            <Input
              id="prop-min"
              inputMode="decimal"
              value={field.min ?? ""}
              onChange={(event) => onUpdate({ min: event.target.value === "" ? null : Number(event.target.value) })}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="prop-max">{t("maxLabel")}</Label>
            <Input
              id="prop-max"
              inputMode="decimal"
              value={field.max ?? ""}
              onChange={(event) => onUpdate({ max: event.target.value === "" ? null : Number(event.target.value) })}
            />
          </div>
        </div>
      )}

      {!isComputed ? (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="prop-unit">{t("unitLabelLabel")}</Label>
          <Input id="prop-unit" value={field.unitLabel ?? ""} onChange={(event) => onUpdate({ unitLabel: event.target.value || null })} />
        </div>
      ) : null}

      {!isComputed ? (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="prop-regex">{t("regexLabel")}</Label>
          <Input id="prop-regex" value={field.regex ?? ""} onChange={(event) => onUpdate({ regex: event.target.value || null })} />
        </div>
      ) : null}

      {isNumeric ? (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="prop-aggregation">{t("rollUpRuleLabel")}</Label>
          <select
            id="prop-aggregation"
            value={field.aggregation}
            onChange={(event) => onUpdate({ aggregation: Number(event.target.value) })}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            {Object.values(AGGREGATION_RULE).map((rule) => (
              <option key={rule} value={rule}>
                {aggregationLabel(rule)}
              </option>
            ))}
          </select>
        </div>
      ) : null}

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="prop-department">{t("ownedByDepartmentLabel")}</Label>
        <select
          id="prop-department"
          value={field.owningDepartmentId ?? ""}
          onChange={(event) => onUpdate({ owningDepartmentId: event.target.value || null })}
          className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
        >
          <option value="">{t("noneOption")}</option>
          {departments.map((department) => (
            <option key={department.id} value={department.id}>
              {department.name}
            </option>
          ))}
        </select>
      </div>

      {isChoice ? (
        <div className="flex flex-col gap-2">
          <Label>{t("optionsLabel")}</Label>
          {(field.options ?? []).map((option) => (
            <div key={option.value} className="flex gap-2">
              <Input
                value={option.label}
                onChange={(event) => onUpdateOption(option.value, { label: event.target.value })}
                aria-label={t("optionLabelFor", { value: option.value })}
              />
              <Button type="button" variant="ghost" size="sm" aria-label={t("removeOption", { label: option.label })} onClick={() => onRemoveOption(option.value)}>
                <Trash2 className="size-4" aria-hidden="true" />
              </Button>
            </div>
          ))}
          <Button type="button" variant="outline" size="sm" className="self-start" onClick={onAddOption}>
            <Plus aria-hidden="true" />
            {t("addOption")}
          </Button>
        </div>
      ) : null}

      {isTable ? (
        <div className="flex flex-col gap-2">
          <Label>{t("columnsLabel")}</Label>
          {(field.columns ?? []).map((column) => (
            <div key={column.key} className="flex gap-2">
              <Input
                value={column.label}
                onChange={(event) => onUpdateColumn(column.key, { label: event.target.value })}
                aria-label={t("columnLabelFor", { label: column.label })}
              />
              <Button type="button" variant="ghost" size="sm" aria-label={t("removeColumn", { label: column.label })} onClick={() => onRemoveColumn(column.key)}>
                <Trash2 className="size-4" aria-hidden="true" />
              </Button>
            </div>
          ))}
          <Button type="button" variant="outline" size="sm" className="self-start" onClick={onAddColumn}>
            <Plus aria-hidden="true" />
            {t("addColumn")}
          </Button>
        </div>
      ) : null}

      <label className="flex items-center gap-2 text-sm text-ink">
        <Checkbox checked={field.required} onCheckedChange={(checked) => onUpdate({ required: checked === true })} />
        {t("requiredLabel")}
      </label>
    </div>
  );
}

function LogicTab({
  field,
  schema,
  onUpdate,
}: {
  field: TemplateField;
  schema: TemplateSchema;
  onUpdate: (patch: Partial<TemplateField>) => void;
}) {
  const t = useTranslations("TemplateBuilder");
  const candidates = schema.sections.flatMap((section) => section.fields).filter((candidate) => candidate.key !== field.key);
  const hasShowIf = field.showIf !== null;

  return (
    <div className="flex flex-col gap-3">
      <label className="flex items-center gap-2 text-sm text-ink">
        <Checkbox
          checked={hasShowIf}
          onCheckedChange={(checked) =>
            onUpdate({ showIf: checked === true ? { fieldKey: candidates[0]?.key ?? "", equalsValue: "" } : null })
          }
        />
        {t("showIfToggleLabel")}
      </label>

      {hasShowIf && field.showIf ? (
        <div className="flex flex-col gap-2 rounded-md bg-ground p-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="prop-showif-field">{t("showIfFieldLabel")}</Label>
            <select
              id="prop-showif-field"
              value={field.showIf.fieldKey}
              onChange={(event) => onUpdate({ showIf: { ...field.showIf!, fieldKey: event.target.value } })}
              className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
            >
              {candidates.map((candidate) => (
                <option key={candidate.key} value={candidate.key}>
                  {candidate.label}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="prop-showif-value">{t("showIfEqualsLabel")}</Label>
            <Input
              id="prop-showif-value"
              value={field.showIf.equalsValue}
              onChange={(event) => onUpdate({ showIf: { ...field.showIf!, equalsValue: event.target.value } })}
            />
          </div>
        </div>
      ) : null}
    </div>
  );
}

function VisibilityTab({
  field,
  levels,
  onUpdate,
}: {
  field: TemplateField;
  levels: UnitLevelSummary[];
  onUpdate: (patch: Partial<TemplateField>) => void;
}) {
  const t = useTranslations("TemplateBuilder");
  const selected = new Set(field.visibleForLevels ?? []);

  const toggleLevel = (level: number, checked: boolean) => {
    const next = new Set(selected);
    if (checked) {
      next.add(level);
    } else {
      next.delete(level);
    }
    onUpdate({ visibleForLevels: next.size === 0 ? null : [...next] });
  };

  return (
    <div className="flex flex-col gap-3">
      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-medium text-ink">{t("visibleForLevelsLabel")}</legend>
        <p className="text-xs text-ink-muted">{t("visibleForLevelsHint")}</p>
        {levels.map((level) => (
          <label key={level.id} className="flex items-center gap-2 text-sm text-ink">
            <Checkbox checked={selected.has(Number(level.code))} onCheckedChange={(checked) => toggleLevel(Number(level.code), checked === true)} />
            {level.name}
          </label>
        ))}
      </fieldset>

      <label className="flex items-center gap-2 text-sm text-ink">
        <Checkbox checked={field.confidential} onCheckedChange={(checked) => onUpdate({ confidential: checked === true })} />
        {t("confidentialLabel")}
      </label>
    </div>
  );
}
