# EmailCrypto Utility

Command-line helper to encrypt or decrypt SMTP passwords using the same AES-GCM logic as the backend (`ISecretProtector`).

## Build & Run

```
dotnet run --project tools/EmailCrypto -- <command> [options]
```

## Commands

### Encrypt

Generate an encrypted password for `appsettings`/environment variables:

```
dotnet run --project tools/EmailCrypto -- encrypt --key <base64-key> --password "<smtp-password>"
```

- `--key` – base64-encoded 32-byte (256-bit) key. Should match `Email:Key`.
- `--password` – plain SMTP password that will be encrypted.

The tool prints the ciphertext to stdout; copy that into `Email:Password` (or `Email__Password` env var).

### Decrypt

Verify an encrypted password or retrieve the plaintext (for debugging/ops only):

```
dotnet run --project tools/EmailCrypto -- decrypt --key <base64-key> --secret "<encrypted-value>"
```

- `--secret` – encrypted value previously produced by `encrypt`.

## Key Management

Use a secure secret store (e.g., 1Password, Azure Key Vault) for the base64 key. Never commit plaintext passwords or keys to source control. Provide the key/password to the backend via environment variables (`Email__Key`, `Email__Password`).
