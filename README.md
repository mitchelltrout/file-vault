# FileVault

An encrypted media vault for Windows. Files live inside an opaque container the
operating system can't see into — filenames, folder structure, and contents all
hidden.

**Stack:** C# · .NET · WPF · Argon2 · AES

## What it does

The design constraint that shapes everything: **plaintext never touches disk.**
Files are decrypted in memory only, so there's no temp file to recover and no
window where the unencrypted copy exists on the filesystem. That rules out the
easy implementation of most features and is most of what makes this interesting.

Passwords are stretched with **Argon2** rather than a plain hash, so brute
force stays expensive.

Beyond the core: a lock screen, a built-in password generator, in-vault video
playback (which has to stream from memory, given the above), and a vault
disguise mode. Split into four projects — `FileVault.Service`,
`FileVault.Shared`, `FileVault.UI`, and `FileVault.Web` — with a Linux web UI
designed later to reach the same vault.

## Running it

Open `FileVault.sln` in Visual Studio and build, or use the helper scripts:

```
run-service.bat    # background service
run-ui.bat         # desktop UI
```

## Status

Working on Windows. Design docs for each phase are in `docs/superpowers/specs/`
— including the threat model behind the memory-only decryption decision.
