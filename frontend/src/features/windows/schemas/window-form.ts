import { z } from "zod";

// Mirrors backend/src/HRS.Application/Features/Windows/SubmissionWindows/{Create,Update}SubmissionWindowCommandValidator.cs.
// graceDays stays z.string() (native inputs are always strings), converted to Number() on submit.

export const dueDateRowSchema = z.object({
  level: z.string().trim().min(1, "Choose a level"),
  dueDate: z.string().trim().min(1, "Due date is required"),
  dueTime: z.string().trim().min(1, "Due time is required"),
});

export const reminderSettingsSchema = z.object({
  remindFiveDaysBefore: z.boolean(),
  remindTwoDaysBefore: z.boolean(),
  remindOnDueDay: z.boolean(),
  escalateToParentAfterThreeDays: z.boolean(),
  escalateToZoneAfterSevenDays: z.boolean(),
});

export const windowFormSchema = z
  .object({
    reportTypeId: z.string().trim().min(1, "Choose a report type"),
    periodLabel: z.string().trim().min(1, "Period label is required").max(100, "Period label is too long"),
    opensDate: z.string().trim().min(1, "Opens date is required"),
    graceDays: z
      .string()
      .trim()
      .min(1, "Enter a grace period")
      .refine((value) => Number.isInteger(Number(value)) && Number(value) >= 0, "Must be a whole number, 0 or more"),
    closesDate: z.string().trim().min(1, "Hard close date is required"),
    dueDates: z.array(dueDateRowSchema).min(1, "Set a due date for at least one level"),
    targetUnitIds: z.array(z.string()),
    reminderSettings: reminderSettingsSchema,
  })
  .refine((values) => new Date(values.closesDate) > new Date(values.opensDate), {
    message: "Hard close must be after the window opens.",
    path: ["closesDate"],
  });

export type WindowFormValues = z.infer<typeof windowFormSchema>;
export type DueDateRowValues = z.infer<typeof dueDateRowSchema>;
