import { describe, expect, it } from "vitest";
import { compressImageIfWorthwhile } from "./compress-image";

function makeFile(name: string, type: string, sizeBytes: number): File {
  return new File([new Uint8Array(sizeBytes)], name, { type });
}

describe("compressImageIfWorthwhile", () => {
  it("leaves non-image files untouched", async () => {
    const file = makeFile("report.pdf", "application/pdf", 1_000_000);

    const result = await compressImageIfWorthwhile(file);

    expect(result).toBe(file);
  });

  it("leaves GIFs untouched to preserve animation", async () => {
    const file = makeFile("meme.gif", "image/gif", 1_000_000);

    const result = await compressImageIfWorthwhile(file);

    expect(result).toBe(file);
  });

  it("leaves small images untouched — not worth the quality loss", async () => {
    const file = makeFile("thumb.jpg", "image/jpeg", 1_000);

    const result = await compressImageIfWorthwhile(file);

    expect(result).toBe(file);
  });

  it("falls back to the original file if compression can't run (e.g. no canvas support)", async () => {
    // jsdom has no createImageBitmap/canvas decoding — this exercises the try/catch fallback
    // path a real browser without it (or a corrupt/unsupported image) would also hit.
    const file = makeFile("site.jpg", "image/jpeg", 1_000_000);

    const result = await compressImageIfWorthwhile(file);

    expect(result).toBe(file);
  });
});
