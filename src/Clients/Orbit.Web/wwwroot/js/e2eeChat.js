// Wraps the Web Crypto API and IndexedDB so OwnEncryptionKeyProvider and Chat.razor can do real
// end-to-end encryption in the browser: Orbit.Api only ever stores and relays ciphertext, and never has
// a key that could decrypt it. Key agreement is ECDH (P-256); messages are encrypted with AES-GCM using
// a key derived from the local private key and the other party's public key.
//
// Scope, documented here so future changes don't accidentally assume more than this provides: a single
// shared key per user pair (not Signal's rotating Double Ratchet - so no per-message forward secrecy),
// no group chats, and the browser trusts whatever public key Orbit.Api currently reports for a user -
// there is no separate identity-verification step (e.g. comparing key fingerprints out of band), so
// this does not protect against a malicious or compromised server substituting a different key.

const databaseName = 'orbit-e2ee';
const keyStoreName = 'keys';

// Record IDs are namespaced by ownUserId so that two different accounts signing into the same browser
// each get their own key pair in IndexedDB instead of one overwriting or reusing the other's.
function ownPrivateKeyRecordId(ownUserId) {
    return `own-private-key:${ownUserId}`;
}

function ownPublicKeyRecordId(ownUserId) {
    return `own-public-key:${ownUserId}`;
}

function openKeyDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(keyStoreName);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function getKeyRecord(recordId) {
    const database = await openKeyDatabase();
    return new Promise((resolve, reject) => {
        const request = database.transaction(keyStoreName, 'readonly').objectStore(keyStoreName).get(recordId);
        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}

async function putKeyRecord(recordId, value) {
    const database = await openKeyDatabase();
    return new Promise((resolve, reject) => {
        const transaction = database.transaction(keyStoreName, 'readwrite');
        transaction.objectStore(keyStoreName).put(value, recordId);
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
    });
}

function arrayBufferToBase64(buffer) {
    return btoa(String.fromCharCode(...new Uint8Array(buffer)));
}

function base64ToUint8Array(base64) {
    return Uint8Array.from(atob(base64), character => character.charCodeAt(0));
}

/// True if this browser already holds a private key for ownUserId - lets OwnEncryptionKeyProvider decide
/// whether a key needs restoring or generating before it can be used, instead of ensureOwnPublicKey
/// silently creating one with no way to back it up.
export async function hasOwnPrivateKey(ownUserId) {
    return (await getKeyRecord(ownPrivateKeyRecordId(ownUserId))) !== null;
}

/// Returns ownUserId's ECDH public key as base64, generating and persisting an extractable private key on
/// first use for that user. Extractable (unlike the original non-extractable design) so it can later be
/// exported for a password-encrypted server-side backup - see wrapOwnPrivateKeyWithPassword.
export async function ensureOwnPublicKey(ownUserId) {
    const existingPublicKeyBase64 = await getKeyRecord(ownPublicKeyRecordId(ownUserId));
    if (existingPublicKeyBase64) {
        return existingPublicKeyBase64;
    }

    const keyPair = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-256' }, true, ['deriveKey']);
    const publicKeyBase64 = arrayBufferToBase64(await crypto.subtle.exportKey('raw', keyPair.publicKey));

    await putKeyRecord(ownPrivateKeyRecordId(ownUserId), keyPair.privateKey);
    await putKeyRecord(ownPublicKeyRecordId(ownUserId), publicKeyBase64);
    return publicKeyBase64;
}

// OWASP's current minimum recommendation for PBKDF2-HMAC-SHA256. Stored alongside each wrapped key
// (see wrapOwnPrivateKeyWithPassword) rather than assumed at restore time, so raising this later doesn't
// invalidate backups wrapped under the old count.
const pbkdf2IterationCount = 600000;

async function derivePasswordWrappingKey(password, saltBytes, iterations) {
    const baseKey = await crypto.subtle.importKey('raw', new TextEncoder().encode(password), 'PBKDF2', false, ['deriveKey']);
    return crypto.subtle.deriveKey(
        { name: 'PBKDF2', salt: saltBytes, iterations, hash: 'SHA-256' }, baseKey, { name: 'AES-GCM', length: 256 }, false,
        ['encrypt', 'decrypt']);
}

/// Exports ownUserId's current private key and encrypts it with a key derived from their account password
/// via PBKDF2, so Orbit.Api can hold a backup it can never read. Returns null instead of throwing when the
/// local key predates extractable keys (see ensureOwnPublicKey) - such a key can never be exported, so
/// there is nothing to back up until it is replaced.
export async function wrapOwnPrivateKeyWithPassword(ownUserId, password) {
    const privateKey = await getKeyRecord(ownPrivateKeyRecordId(ownUserId));
    if (!privateKey) {
        throw new Error('No local private key for this user - call ensureOwnPublicKey first.');
    }

    let privateKeyJwk;
    try {
        privateKeyJwk = await crypto.subtle.exportKey('jwk', privateKey);
    } catch {
        return null;
    }

    const salt = crypto.getRandomValues(new Uint8Array(16));
    const nonce = crypto.getRandomValues(new Uint8Array(12));
    const wrappingKey = await derivePasswordWrappingKey(password, salt, pbkdf2IterationCount);
    const ciphertext = await crypto.subtle.encrypt(
        { name: 'AES-GCM', iv: nonce }, wrappingKey, new TextEncoder().encode(JSON.stringify(privateKeyJwk)));

    return {
        ciphertextBase64: arrayBufferToBase64(ciphertext),
        nonceBase64: arrayBufferToBase64(nonce),
        saltBase64: arrayBufferToBase64(salt),
        iterations: pbkdf2IterationCount
    };
}

/// Reverses wrapOwnPrivateKeyWithPassword: decrypts wrapped with a key derived from password, imports the
/// recovered private key into this browser's IndexedDB (extractable, so it can be wrapped again later),
/// and re-derives the matching public key from it. Returns the restored public key as base64, or null if
/// the password is wrong or the backup is corrupted, rather than throwing - the caller falls back to
/// generating a brand-new key pair in that case.
export async function restoreOwnPrivateKeyFromBackup(ownUserId, password, wrapped) {
    try {
        const salt = base64ToUint8Array(wrapped.saltBase64);
        const wrappingKey = await derivePasswordWrappingKey(password, salt, wrapped.iterations);
        const plainTextBuffer = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: base64ToUint8Array(wrapped.nonceBase64) }, wrappingKey,
            base64ToUint8Array(wrapped.ciphertextBase64));
        const privateKeyJwk = JSON.parse(new TextDecoder().decode(plainTextBuffer));

        const privateKey = await crypto.subtle.importKey(
            'jwk', privateKeyJwk, { name: 'ECDH', namedCurve: 'P-256' }, true, ['deriveKey']);

        // The public key isn't part of the backup - it's cheap to re-derive from the private key's JWK
        // coordinates (x/y) instead of also storing and trusting a separate copy of it server-side.
        const publicKeyJwk = { ...privateKeyJwk };
        delete publicKeyJwk.d;
        delete publicKeyJwk.key_ops;
        const publicKey = await crypto.subtle.importKey('jwk', publicKeyJwk, { name: 'ECDH', namedCurve: 'P-256' }, true, []);
        const publicKeyBase64 = arrayBufferToBase64(await crypto.subtle.exportKey('raw', publicKey));

        await putKeyRecord(ownPrivateKeyRecordId(ownUserId), privateKey);
        await putKeyRecord(ownPublicKeyRecordId(ownUserId), publicKeyBase64);
        return publicKeyBase64;
    } catch {
        return null;
    }
}

async function deriveSharedKey(ownUserId, otherPartyPublicKeyBase64) {
    const ownPrivateKey = await getKeyRecord(ownPrivateKeyRecordId(ownUserId));
    if (!ownPrivateKey) {
        throw new Error('No local private key for this user - call ensureOwnPublicKey first.');
    }

    const otherPartyPublicKey = await crypto.subtle.importKey(
        'raw', base64ToUint8Array(otherPartyPublicKeyBase64), { name: 'ECDH', namedCurve: 'P-256' }, false, []);

    return crypto.subtle.deriveKey(
        { name: 'ECDH', public: otherPartyPublicKey }, ownPrivateKey, { name: 'AES-GCM', length: 256 }, false,
        ['encrypt', 'decrypt']);
}

/// Encrypts plainText, sent by ownUserId, for the holder of otherPartyPublicKeyBase64. Returns
/// { ciphertextBase64, nonceBase64 } - the random 12-byte AES-GCM nonce must travel alongside the
/// ciphertext, since decryption needs the exact same value.
export async function encryptMessage(ownUserId, otherPartyPublicKeyBase64, plainText) {
    const sharedKey = await deriveSharedKey(ownUserId, otherPartyPublicKeyBase64);
    const nonce = crypto.getRandomValues(new Uint8Array(12));
    const ciphertext = await crypto.subtle.encrypt(
        { name: 'AES-GCM', iv: nonce }, sharedKey, new TextEncoder().encode(plainText));

    return {
        ciphertextBase64: arrayBufferToBase64(ciphertext),
        nonceBase64: arrayBufferToBase64(nonce)
    };
}

/// Reverses encryptMessage for ownUserId, the recipient. Returns null instead of throwing when
/// decryption fails (e.g. a message encrypted for a since-replaced key pair), so the caller can render a
/// placeholder instead of crashing the chat window.
export async function decryptMessage(ownUserId, otherPartyPublicKeyBase64, ciphertextBase64, nonceBase64) {
    try {
        const sharedKey = await deriveSharedKey(ownUserId, otherPartyPublicKeyBase64);
        const plainTextBuffer = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: base64ToUint8Array(nonceBase64) }, sharedKey, base64ToUint8Array(ciphertextBase64));
        return new TextDecoder().decode(plainTextBuffer);
    } catch {
        return null;
    }
}
