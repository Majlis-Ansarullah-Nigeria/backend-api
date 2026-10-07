// Matches backend/src/HRS.Domain/Templates/{TemplateFieldType,AggregationRule}.cs's enum order —
// the API serializes both as plain numbers (orval only generates `type X = number`, same as
// features/templates/utils/report-type.ts's ReportTypeCategory/Frequency constants).

export const TEMPLATE_FIELD_TYPE = {
  ShortText: 0,
  LongText: 1,
  Number: 2,
  Currency: 3,
  Percentage: 4,
  Date: 5,
  DateRange: 6,
  YesNo: 7,
  SingleChoice: 8,
  MultipleChoice: 9,
  Dropdown: 10,
  Rating: 11,
  FileUpload: 12,
  RepeatableTable: 13,
  MemberList: 14,
  Location: 15,
  Computed: 16,
} as const;

export const AGGREGATION_RULE = {
  None: 0,
  Sum: 1,
  Average: 2,
  Min: 3,
  Max: 4,
  Latest: 5,
} as const;

export const NUMERIC_FIELD_TYPES: readonly number[] = [
  TEMPLATE_FIELD_TYPE.Number,
  TEMPLATE_FIELD_TYPE.Currency,
  TEMPLATE_FIELD_TYPE.Percentage,
  TEMPLATE_FIELD_TYPE.Rating,
];

export const TEXT_FIELD_TYPES: readonly number[] = [TEMPLATE_FIELD_TYPE.ShortText, TEMPLATE_FIELD_TYPE.LongText];
