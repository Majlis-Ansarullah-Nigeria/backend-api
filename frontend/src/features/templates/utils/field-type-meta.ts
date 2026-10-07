import {
  Calculator,
  Calendar,
  CalendarRange,
  CheckSquare,
  CircleDot,
  Hash,
  ListChecks,
  MapPin,
  Percent,
  Star,
  Table,
  ToggleLeft,
  Type,
  Upload,
  Users,
  WalletCards,
  type LucideIcon,
} from "lucide-react";
import { AGGREGATION_RULE, TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";

export interface FieldTypeMeta {
  label: string;
  icon: LucideIcon;
}

export const FIELD_TYPE_META: Record<number, FieldTypeMeta> = {
  [TEMPLATE_FIELD_TYPE.ShortText]: { label: "Short text", icon: Type },
  [TEMPLATE_FIELD_TYPE.LongText]: { label: "Long text", icon: ListChecks },
  [TEMPLATE_FIELD_TYPE.Number]: { label: "Number", icon: Hash },
  [TEMPLATE_FIELD_TYPE.Currency]: { label: "Currency", icon: WalletCards },
  [TEMPLATE_FIELD_TYPE.Percentage]: { label: "Percentage", icon: Percent },
  [TEMPLATE_FIELD_TYPE.Date]: { label: "Date", icon: Calendar },
  [TEMPLATE_FIELD_TYPE.DateRange]: { label: "Date range", icon: CalendarRange },
  [TEMPLATE_FIELD_TYPE.YesNo]: { label: "Yes / No", icon: ToggleLeft },
  [TEMPLATE_FIELD_TYPE.SingleChoice]: { label: "Single choice", icon: CircleDot },
  [TEMPLATE_FIELD_TYPE.MultipleChoice]: { label: "Multiple choice", icon: CheckSquare },
  [TEMPLATE_FIELD_TYPE.Dropdown]: { label: "Dropdown", icon: ListChecks },
  [TEMPLATE_FIELD_TYPE.Rating]: { label: "Rating", icon: Star },
  [TEMPLATE_FIELD_TYPE.FileUpload]: { label: "File / image upload", icon: Upload },
  [TEMPLATE_FIELD_TYPE.RepeatableTable]: { label: "Repeating table", icon: Table },
  [TEMPLATE_FIELD_TYPE.MemberList]: { label: "Member list", icon: Users },
  [TEMPLATE_FIELD_TYPE.Location]: { label: "Location", icon: MapPin },
  [TEMPLATE_FIELD_TYPE.Computed]: { label: "Calculated", icon: Calculator },
};

export const FIELD_TYPE_ORDER: readonly number[] = [
  TEMPLATE_FIELD_TYPE.ShortText,
  TEMPLATE_FIELD_TYPE.LongText,
  TEMPLATE_FIELD_TYPE.Number,
  TEMPLATE_FIELD_TYPE.Currency,
  TEMPLATE_FIELD_TYPE.Percentage,
  TEMPLATE_FIELD_TYPE.Date,
  TEMPLATE_FIELD_TYPE.DateRange,
  TEMPLATE_FIELD_TYPE.YesNo,
  TEMPLATE_FIELD_TYPE.SingleChoice,
  TEMPLATE_FIELD_TYPE.MultipleChoice,
  TEMPLATE_FIELD_TYPE.Dropdown,
  TEMPLATE_FIELD_TYPE.Rating,
  TEMPLATE_FIELD_TYPE.FileUpload,
  TEMPLATE_FIELD_TYPE.RepeatableTable,
  TEMPLATE_FIELD_TYPE.MemberList,
  TEMPLATE_FIELD_TYPE.Location,
  TEMPLATE_FIELD_TYPE.Computed,
];

export function fieldTypeLabel(type: number): string {
  return FIELD_TYPE_META[type]?.label ?? "Unknown";
}

const AGGREGATION_LABELS: Record<number, string> = {
  [AGGREGATION_RULE.None]: "None",
  [AGGREGATION_RULE.Sum]: "Sum up the hierarchy",
  [AGGREGATION_RULE.Average]: "Average across the hierarchy",
  [AGGREGATION_RULE.Min]: "Lowest in the hierarchy",
  [AGGREGATION_RULE.Max]: "Highest in the hierarchy",
  [AGGREGATION_RULE.Latest]: "Latest value",
};

export function aggregationLabel(rule: number): string {
  return AGGREGATION_LABELS[rule] ?? "Unknown";
}

export const NUMERIC_AGGREGATABLE_TYPES: readonly number[] = [
  TEMPLATE_FIELD_TYPE.Number,
  TEMPLATE_FIELD_TYPE.Currency,
  TEMPLATE_FIELD_TYPE.Percentage,
];
