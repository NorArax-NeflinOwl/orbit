// Bytes the app fetched itself, drawn by an <img>. A picture in a note cannot be a plain src="/api/..." -
// an <img> sends no bearer token, and a sealed picture's bytes are ciphertext until this browser opens
// them - so the bytes arrive through the app's own HTTP client, are opened where they have to be, and are
// handed to the document as a blob: URL the page owns and revokes. See NotePictureSource.

/// A URL for these bytes, good until revoked.
export function create(bytes, contentType) {
    return URL.createObjectURL(new Blob([bytes], { type: contentType || 'application/octet-stream' }));
}

export function revoke(url) {
    if (url) {
        URL.revokeObjectURL(url);
    }
}
