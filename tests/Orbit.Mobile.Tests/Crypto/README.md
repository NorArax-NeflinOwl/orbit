# Cross-platform E2EE test vectors

`browser-e2ee-vectors.json` was produced **by a real browser running Orbit.Web's own
`wwwroot/js/e2eeChat.js`**, not by a re-implementation. That is the whole point: Orbit.Maui has to
interoperate byte-for-byte with ciphertext the web client produces, and a vector generated from a second
implementation of the spec would only prove that implementation agrees with itself.

Regenerate with `generate-browser-vectors.html`: copy it and `e2eeChat.js` into a directory, serve it
over HTTP (the module needs a real origin for IndexedDB), and open it. It prints the JSON.

## About the keys in this file

The private keys here are **throwaway, generated for this file, and used by nothing**. They are test
data in the same sense as a NIST or RFC test vector, and they must be committed - a fixed vector that
nobody can reproduce fixes nothing. They are not credentials for any Orbit account, deployment, or
service, and no real user's key is ever checked in.

`backupPassword` is the passphrase those test keys are wrapped under, for the same reason.

## Checking the other direction

The vectors prove .NET can read what a browser wrote. Nothing in a .NET test can prove the reverse -
only a browser can say whether WebCrypto accepts what this side produces - so that half is a two-step
check:

1. `dotnet test` (DotNetOutputTests) writes `dotnet-produced.json` next to the test binary.
2. Copy it, `e2eeChat.js`, and `verify-dotnet-output-in-a-browser.html` into one directory, serve over
   HTTP, and open the page. Every field it prints should be `true`.

It checks four things: a browser can restore a key from a backup, decrypt a message .NET encrypted,
open content .NET sealed for one reader, and import a JWK backup .NET wrote.

## What each vector pins down

| Field | What it proves |
| --- | --- |
| `rawSharedSecretEqualsAesKey` | Asserted **in the browser** at generation time: WebCrypto's `deriveKey(ECDH → AES-GCM, 256)` uses the raw ECDH shared secret with **no KDF**. This is the detail the plan (§4.1) calls the largest correctness risk in the project - `.NET`'s `DeriveKeyFromHash`/`DeriveKeyFromHmac` all apply a KDF and are therefore wrong. |
| `sharedSecretBase64` | The value `ECDiffieHellman.DeriveRawSecretAgreement` must return for these two keys. |
| `alice.backup`, `bob.backup` | A JWK private key wrapped with PBKDF2-HMAC-SHA256 at 600,000 iterations, as `wrapOwnPrivateKeyWithPassword` produces it. |
| `aliceToBob`, `bobToAlice` | Real messages from `encryptMessage`. WebCrypto appends the 16-byte GCM tag to the ciphertext; .NET's `AesGcm` wants it separately. |
| `aliceToSelf` | `encryptForSelf` - the one-reader form used by private notes, which runs the same agreement with the user's own key on both sides. |
