import { z } from "zod";

export const passwordLoginSchema = z.object({
  identifier: z.string().trim().min(1, "Enter your email or phone number."),
  password: z.string().min(1, "Enter your password."),
});

export type PasswordLoginFormValues = z.infer<typeof passwordLoginSchema>;

export const codeLoginSchema = z.object({
  identifier: z.string().trim().min(1, "Enter your email or phone number."),
});

export type CodeLoginFormValues = z.infer<typeof codeLoginSchema>;

export const forgotPasswordSchema = z.object({
  identifier: z.string().trim().min(1, "Enter your email or phone number."),
});

export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;
