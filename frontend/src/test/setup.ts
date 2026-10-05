import "@testing-library/jest-dom/vitest";

// jsdom doesn't implement ResizeObserver; Radix primitives (e.g. Checkbox, via
// react-use-size) read it on mount regardless of whether a test touches sizing.
if (typeof globalThis.ResizeObserver === "undefined") {
  globalThis.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}
