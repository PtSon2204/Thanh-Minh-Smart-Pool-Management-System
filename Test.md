## Integration tests

Install .NET 8, Docker Desktop, and FFmpeg. Run Docker Desktop with Linux containers.
FFmpeg needs the `libx264` and `libx265` encoders for CCTV stream tests.
These are different from browser QR-scanner camera tests.

Install FFmpeg once on Windows, then reopen PowerShell:

```powershell
winget install --id Gyan.FFmpeg.Essentials --exact
```

If FFmpeg is not on PATH, supply its full path:

```powershell
.\test-integration.ps1 -Ffmpeg "C:\tools\ffmpeg\bin\ffmpeg.exe"
```

For one feature during development, an optional filter keeps the run small:

```powershell
.\test-integration.ps1 -Filter "FullyQualifiedName~RentalWorkflowIntegrationTests"
```

### Dotnet test

Start the dedicated test services once:

```powershell
docker compose -f compose.tests.yml up -d --wait
```

Then use the normal command from the repository root. No filters or connection exports are required:

```powershell
dotnet test backend/tests/SmartPool.IntegrationTests
```

To test both backend projects:

```powershell
dotnet test backend/SmartPoolManagementSystem.sln
```

Stop and remove only the dedicated test services when finished:

```powershell
docker compose -f compose.tests.yml down --volumes
```

PostgreSQL test data uses container memory storage. Never change the test connection to `smartpool_dev`.
An explicitly configured development database is rejected before connection.

### Test configuration and overrides

The checked-in password is only for disposable local and CI test containers. It is not an application credential.
The automatic runner and CI may override the defaults with process environment variables:

| Setting | Default for raw tests |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Local test PostgreSQL on port `55439`, database `smartpool_test` |
| `SMARTPOOL_TEST_BOOTSTRAP` | `true`; creates test schema, extensions, roles, and operator |
| `SMARTPOOL_TEST_REDIS_CONNECTION` | Local test Redis on port `56379` |
| `SMARTPOOL_CAMERA_TEST_FFMPEG` | Resolve `ffmpeg` from PATH |

If you previously exported old connection variables, open a new terminal or remove those stale variables first.
Explicit environment values take priority and are not silently replaced.
`SMARTPOOL_TEST_ENV_PATH` remains available only when you explicitly select a test environment file.
Database safety checks still apply to that file.
