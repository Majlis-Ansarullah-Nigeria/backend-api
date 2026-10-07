/** "a.adeyemi@example.org" -> "a.ade…@example.org" — matches the "Check your email" mockup. */
export function maskEmail(email: string): string {
  const [local, domain] = email.split("@");
  if (!local || !domain) {
    return email;
  }
  const visible = local.slice(0, Math.min(5, local.length));
  return `${visible}…@${domain}`;
}
