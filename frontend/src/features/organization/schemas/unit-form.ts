import { z } from "zod";

// Mirrors backend/src/HRS.Application/Features/Organization/Units/{Create,Update}UnitCommandValidator.cs.
// The server is authoritative; this is for live UX feedback only. Blank-to-null conversion
// happens in each form's onSubmit, same as AcceptInvitationForm's phone field.

const EMAIL_FIELD = z.string().trim().email("Enter a valid email address").optional().or(z.literal(""));

const coordinateField = (min: number, max: number) =>
  z
    .string()
    .trim()
    .optional()
    .or(z.literal(""))
    .refine(
      (value) => !value || (!Number.isNaN(Number(value)) && Number(value) >= min && Number(value) <= max),
      { message: `Must be between ${min} and ${max}` },
    );

export const updateUnitSchema = z.object({
  name: z.string().trim().min(1, "Name is required").max(200, "Name is too long"),
  address: z.string().trim().optional().or(z.literal("")),
  contactPhone: z.string().trim().optional().or(z.literal("")),
  contactEmail: EMAIL_FIELD,
  latitude: coordinateField(-90, 90),
  longitude: coordinateField(-180, 180),
});

export type UpdateUnitFormValues = z.infer<typeof updateUnitSchema>;

export const createUnitSchema = z.object({
  code: z.string().trim().min(1, "Code is required").max(20, "Code is too long"),
  name: z.string().trim().min(1, "Name is required").max(200, "Name is too long"),
  levelId: z.string().trim().min(1, "Choose a level"),
  parentId: z.string().trim().min(1, "Choose a parent"),
  address: z.string().trim().optional().or(z.literal("")),
  contactPhone: z.string().trim().optional().or(z.literal("")),
  contactEmail: EMAIL_FIELD,
  latitude: coordinateField(-90, 90),
  longitude: coordinateField(-180, 180),
});

export type CreateUnitFormValues = z.infer<typeof createUnitSchema>;
