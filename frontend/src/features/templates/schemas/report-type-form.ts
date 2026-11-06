import { z } from "zod";

// Mirrors backend/src/HRS.Application/Features/Templates/ReportTypes/{Create,Update}ReportTypeCommandValidator.cs.
// Category/frequency/recipientLevel stay z.string() (not z.coerce.number()) because native
// <select> values are always strings in react-hook-form — converted to Number() on submit,
// same reasoning as role-name.ts's RoleName field.

export const reportTypeFormSchema = z.object({
  name: z.string().trim().min(1, "Name is required").max(200, "Name is too long"),
  // FR-SUB-010: starts every reference number. Optional — left blank, the server assigns the next
  // free code for the category (GMR, GMR2…). Upper-cased on submit, like the server does.
  code: z
    .string()
    .trim()
    .regex(/^([A-Za-z][A-Za-z0-9]{1,5})?$/, "Use 2–6 letters or digits, starting with a letter")
    .optional()
    .or(z.literal("")),
  description: z.string().trim().max(2000, "Description is too long").optional().or(z.literal("")),
  category: z.string().trim().min(1, "Choose a category"),
  frequency: z.string().trim().min(1, "Choose a frequency"),
  applicableLevels: z.array(z.string()).min(1, "Choose at least one level"),
  recipientLevel: z.string().trim().min(1, "Choose a recipient level"),
  observerDepth: z
    .string()
    .trim()
    .min(1, "Enter an observer depth")
    .refine((value) => Number.isInteger(Number(value)) && Number(value) >= 0, "Must be a whole number, 0 or more"),
  allowAttachments: z.boolean(),
  allowNilReport: z.boolean(),
});

export type ReportTypeFormValues = z.infer<typeof reportTypeFormSchema>;
