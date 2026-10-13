// FR-NOT-001 web push service worker. Deliberately minimal: it only shows a notification for
// whatever payload the server sent and focuses/opens the client at its link on click — no
// caching, no offline strategy (the app's own offline queue for report drafts, NFR-RES-01,
// is unrelated and lives entirely in IndexedDB on the main thread).

self.addEventListener("push", (event) => {
  let payload = { title: "Notification", body: "", link: "/notifications" };
  try {
    if (event.data) {
      payload = { ...payload, ...event.data.json() };
    }
  } catch {
    // Malformed payload: fall back to the generic message above rather than show nothing.
  }

  event.waitUntil(
    self.registration.showNotification(payload.title, {
      body: payload.body,
      data: { link: payload.link || "/notifications" },
    }),
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const link = event.notification.data?.link || "/notifications";

  event.waitUntil(
    (async () => {
      const clientsList = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
      const existing = clientsList.find((client) => new URL(client.url).pathname === link);
      if (existing) {
        await existing.focus();
        return;
      }
      await self.clients.openWindow(link);
    })(),
  );
});
