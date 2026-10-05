import { z } from "zod";

// Mirrors backend/src/HRS.Infrastructure/DependencyInjection.cs's password policy
// (length + digit) and AcceptInvitationCommandValidator's phone/consent rules. The
// "not a common password" check is enforced authoritatively by the server
// (CommonPasswordValidator); this short list is only for live UX feedback.
export const PASSWORD_MIN_LENGTH = 12;

const COMMON_PASSWORDS = new Set([
  "password1234",
  "password123!",
  "12345678901234",
  "123456789012",
  "qwertyuiop123",
  "letmein123456",
  "welcome123456",
  "iloveyou12345",
  "administrator1",
  "changeme12345",
  "correcthorse12",
  "abcdefghijklmn",
  "trustno1234567",
  "football123456",
  "baseball123456",
  "superman123456",
  "princess123456",
  "sunshine123456",
  "dragonball1234",
  "whateveriwant1",
]);

export function passwordCriteria(password: string) {
  return {
    length: password.length >= PASSWORD_MIN_LENGTH,
    digit: /\d/.test(password),
    notCommon: password.length === 0 || !COMMON_PASSWORDS.has(password.toLowerCase()),
  };
}

const PHONE_PATTERN = /^\+?[0-9 ()-]{7,20}$/;

export const acceptInvitationSchema = z.object({
  name: z.string().trim().min(1, "Enter your full name."),
  phone: z
    .string()
    .trim()
    .regex(PHONE_PATTERN, "Enter a valid phone number.")
    .optional()
    .or(z.literal("")),
  password: z
    .string()
    .min(PASSWORD_MIN_LENGTH, `Use at least ${PASSWORD_MIN_LENGTH} characters.`)
    .regex(/\d/, "Include at least one number."),
  agreedToPrivacyNotice: z
    .boolean()
    .refine((value) => value === true, { message: "You must agree to the privacy notice." }),
});

export type AcceptInvitationFormValues = z.infer<typeof acceptInvitationSchema>;
