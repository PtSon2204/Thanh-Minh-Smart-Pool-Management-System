# Local test instructions

## Requirements

- .NET 8 SDK.
- Docker Desktop with Linux containers for integration tests.
- FFmpeg with `libx264` and `libx265` for CCTV camera integration tests.
- Node.js 20 for frontend tests.

The CCTV camera tests are different from the browser QR-scanner camera tests.
The CCTV tests read `SMARTPOOL_CAMERA_TEST_FFMPEG` directly. Set it to the FFmpeg executable before running those tests:

```powershell
$env:SMARTPOOL_CAMERA_TEST_FFMPEG = (Get-Command ffmpeg -CommandType Application).Source
```

You can set an explicit path if FFmpeg is not on PATH:

```powershell
$env:SMARTPOOL_CAMERA_TEST_FFMPEG = 'C:\tools\ffmpeg\bin\ffmpeg.exe'
```

## Backend integration tests

Start the dedicated PostgreSQL and Redis test services:

```powershell
docker compose -f compose.tests.yml up -d --wait
```

Run one integration test class:

```powershell
dotnet test backend/tests/SmartPool.IntegrationTests --filter "FullyQualifiedName~RentalWorkflowIntegrationTests"
```

Run all integration tests. Camera tests need the FFmpeg environment variable above:

```powershell
dotnet test backend/tests/SmartPool.IntegrationTests
```

Run both backend test projects:

```powershell
dotnet test backend/SmartPoolManagementSystem.sln
```

Stop and remove only the dedicated test services when finished:

```powershell
docker compose -f compose.tests.yml down --volumes
```

The PostgreSQL test data uses container memory storage. Never change the test connection to `smartpool_dev`.
The database safety checks reject an explicitly configured development database before connecting.

## Test configuration and overrides

`appsettings.IntegrationTests.json` provides test defaults. Process environment variables override those defaults.

| Setting | Default |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Local test PostgreSQL on port `55439`, database `smartpool_test` |
| `SMARTPOOL_TEST_BOOTSTRAP` | `true`; creates test schema, extensions, roles, and operator |
| `SMARTPOOL_TEST_REDIS_CONNECTION` | Local test Redis on port `56379` |
| `SMARTPOOL_CAMERA_TEST_FFMPEG` | No default; required by CCTV camera integration tests |

The checked-in database password is only for disposable local and CI test containers.
If you exported old connection variables, open a new terminal or remove those variables first.
Explicit environment values take priority and are not silently replaced.
`SMARTPOOL_TEST_ENV_PATH` is available only when you explicitly select a test environment file.
Database safety checks also apply to that file.

## Frontend tests

Run the frontend unit tests from the frontend project:

```powershell
npm --prefix frontend/thanh-minh-smart-pool test
```
