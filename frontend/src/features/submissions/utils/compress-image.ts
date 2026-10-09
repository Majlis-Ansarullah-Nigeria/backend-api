const MAX_DIMENSION = 1920;
const JPEG_QUALITY = 0.8;
const SKIP_BELOW_BYTES = 300_000;

/**
 * Client-side image compression (FR-SUB-005) — downscales to a max 1920px edge and re-encodes as
 * JPEG, using the browser's own canvas (no new dependency). Skips GIFs (re-encoding would drop
 * animation) and already-small files (not worth the quality loss for negligible size gain).
 * Returns the original file unchanged if compression doesn't actually help or isn't applicable.
 */
export async function compressImageIfWorthwhile(file: File): Promise<File> {
  if (!file.type.startsWith("image/") || file.type === "image/gif" || file.size <= SKIP_BELOW_BYTES) {
    return file;
  }

  try {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, MAX_DIMENSION / Math.max(bitmap.width, bitmap.height));
    const width = Math.round(bitmap.width * scale);
    const height = Math.round(bitmap.height * scale);

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) {
      return file;
    }
    context.drawImage(bitmap, 0, 0, width, height);

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", JPEG_QUALITY));
    if (!blob || blob.size >= file.size) {
      return file;
    }

    const newName = file.name.replace(/\.\w+$/, "") + ".jpg";
    return new File([blob], newName, { type: "image/jpeg", lastModified: file.lastModified });
  } catch {
    // Any failure (unsupported format, decode error) falls back to uploading the original.
    return file;
  }
}
