# Security configuration

Do not commit passwords, API tokens, connection strings, or JWT signing keys. Configure them through environment variables or a secrets manager.

For local development, the .NET configuration key separator is written as `__` in environment variables. Required examples:

```powershell
$env:ConnectionStrings__MasterDbConnection = "Server=localhost;Database=POS_Master;Uid=...;Pwd=..."
$env:Jwt__Key = "a-random-secret-with-at-least-32-characters"
```

The POS API only accepts origins listed in `Cors:AllowedOrigins`. Add deployed UI URLs through configuration; `http://localhost:5173` is provided for local Vite development.

To initialize a new web-admin database, set the following values once. Remove them after the first administrator has been created:

```powershell
$env:BootstrapAdmin__Username = "admin"
$env:BootstrapAdmin__Password = "use-a-unique-strong-password"
```

Any credentials previously committed to this repository must be revoked and replaced, including database passwords, Dropbox credentials, and Telegram bot tokens.
