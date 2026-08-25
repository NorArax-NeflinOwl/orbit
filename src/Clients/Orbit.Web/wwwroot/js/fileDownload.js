// Hands the browser a file to save. Blazor WebAssembly can build the bytes but cannot start a download
// on its own, so this makes a blob URL, clicks a link at it, and revokes the URL again - the object URL
// holds the whole file in memory until it is revoked, which matters for an export of a full account.
export function downloadText(fileName, contentType, text) {
    const blob = new Blob([text], { type: contentType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}
