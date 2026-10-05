import { getRequestConfig } from "next-intl/server";

const locale = "en";

export default getRequestConfig(async () => ({
  locale,
  messages: (await import(`../../messages/${locale}.json`)).default,
}));
