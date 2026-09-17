# Employee Profile Management API

GTS Poland take-home assignment: a REST API for managing employee profiles (CRUD) with a bulk CSV import.

**Stack:** C# / .NET 10, ASP.NET Core Web API (controllers), PostgreSQL 16, EF Core 10 (Npgsql), FluentValidation, CsvHelper, xUnit.

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), running
- EF Core CLI tools:

```bash
dotnet tool install --global dotnet-ef
```

If they are already installed, make sure they are current: `dotnet tool update --global dotnet-ef`

### 1. Start PostgreSQL

Docker Desktop has to be running first — `docker compose` talks to its engine and
fails with a connection error if it is not started.

```bash
docker compose up -d
```

The database is exposed on host port **5433**, not the default 5432, so it does
not clash with a PostgreSQL instance you may already have installed locally.

On the first run Docker pulls the `postgres:16` image, which takes a moment.
Wait until the container reports `healthy` before continuing:

```bash
docker compose ps
```

### 2. Apply migrations

```bash
dotnet ef database update --project src/EmployeeManagement.Api
```

EF Core logs a failed `SELECT` against `__EFMigrationsHistory` on a brand-new
database. That is expected — the table does not exist yet, and EF creates it
before applying the migration. The command is successful if it ends with `Done.`

### 3. Run the API

```bash
dotnet run --project src/EmployeeManagement.Api
```

Then open the interactive API reference at the URL printed in the console, for
example `http://localhost:5027/scalar/v1`.

### Running the tests

```bash
dotnet test
```

Tests use an in-memory database and do not require Docker or a running API.

## Project structure

One API project split into folders. The solution file is `EmployeeManagement.slnx`
(.NET 10 generates the XML solution format).

```
src/EmployeeManagement.Api/
  Controllers/    EmployeesController - routing, status codes, no business logic
  Services/       EmployeeService - business logic; ServiceResult/ServiceError
                  carry the expected outcomes (validation, not found, conflict)
  Validators/     FluentValidation rules; a second validator for PUT adds the
                  Status requirement; ValidatorKeys names the keyed registrations
  Dtos/           request/response contracts and the input normalizer
  Import/         EmployeeCsvReader and EmployeeCsvRow - CSV parsing only,
                  kept apart from the import logic in the service
  Models/         the Employee entity and the EmployeeStatus enum
  Data/           AppDbContext - the unique index and column lengths
  Migrations/     EF Core migrations
  ErrorHandling/  GlobalExceptionHandler - unexpected failures in one place

tests/EmployeeManagement.Tests/
  Validators/     business rule tests
  Services/       update logic and CSV import tests (EF Core InMemory)
  Endpoints/      end-to-end tests through WebApplicationFactory
  TestHelpers/    fixed TimeProvider, shared test data, the test host

samples/          the CSV file provided with the assignment
docker-compose.yml
```

## API

| Method | Route | Success | Errors |
|---|---|---|---|
| POST | `/employee` | 201 + `Location` | 400 validation, 409 email taken |
| GET | `/employee/{id}` | 200 | 404 |
| GET | `/employees` | 200 | |
| PUT | `/employee/{id}` | 200 (updated employee) | 400, 404, 409 |
| DELETE | `/employee/{id}` | 204 | 404 |
| POST | `/employees/bulk` | 200 + per-row report | 400 unusable file, 413 file > 5 MB |

All errors use RFC 9457 Problem Details (`application/problem+json`). Validation errors list every invalid field at once. Unexpected errors return 500 without internals; a database that is temporarily unreachable returns 503.

Ready-to-run requests: [`src/EmployeeManagement.Api/EmployeeManagement.Api.http`](src/EmployeeManagement.Api/EmployeeManagement.Api.http).

```bash
# Create (Status omitted -> Active)
curl -i -X POST http://localhost:5027/employee \
  -H "Content-Type: application/json" \
  -d '{"name":"Anna Kowalska","hireDate":"2024-03-01","email":"Anna.Kowalska@Example.com","phoneNo":"+48 123 456 789","city":"Lublin","country":"Poland","pincode":"20-001"}'

# List
curl http://localhost:5027/employees

# Update (full replace - Status required)
curl -i -X PUT http://localhost:5027/employee/{id} \
  -H "Content-Type: application/json" \
  -d '{"name":"Anna Nowak","hireDate":"2024-03-01","email":"anna.nowak@example.com","phoneNo":"+48123456789","status":"Inactive"}'

# Delete
curl -i -X DELETE http://localhost:5027/employee/{id}

# Bulk import of the provided file
curl -X POST http://localhost:5027/employees/bulk -F "file=@samples/employees_sample.csv"
```

Example validation error:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "HireDate": ["HireDate cannot be in the future."],
    "Status": ["Status must be one of: Active, Inactive."]
  }
}
```

### Smoke test

Everything below was run manually against Scalar and a real database. It doubles
as a quick way to check a fresh clone.

| # | Request | Body / file | Expected |
|---|---|---|---|
| 1 | `GET /employees` | | `200` and `[]` on an empty database |
| 2 | `POST /employee` | `{"name":"Anna Kowalska","email":"Anna.Kowalska@Example.COM","phoneNo":"+48 501-234-567","hireDate":"2024-03-01"}` | `201`, `Location` header, `email` stored as `anna.kowalska@example.com`, `phoneNo` as `+48501234567`, `status` defaulted to `Active` |
| 3 | `GET /employee/{id}` | id from step 2 | `200` with the same employee |
| 4 | `POST /employee` | same body, email upper-cased | `409` — uniqueness ignores case |
| 5 | `POST /employee` | `{"name":"","email":"not-an-email","phoneNo":"501234567","hireDate":"2030-01-01","status":"Zombie","profilePicture":"ftp://x.pl/a.png"}` | `400` listing all six fields at once |
| 6 | `POST /employee` | `"hireDate":"2222-22-11"` | `400` — rejected during model binding, before the validators run |
| 7 | `PUT /employee/{id}` | full body without `status` | `400` — Status is required on update |
| 8 | `PUT /employee/{id}` | full body with `"status":"inactive"` | `200`, stored as `Inactive`; `id` and `createdAt` unchanged |
| 9 | `PUT /employee/{unknown guid}` | any valid body | `404` |
| 10 | `DELETE /employee/{id}` then `GET` | | `204`, then `404` |
| 11 | `POST /employees/bulk` | `samples/employees_sample.csv` in form field `file` | `200`, 6 imported, 4 rejected (lines 3, 8, 9, 11) |
| 12 | `POST /employees/bulk` | the same file again | `200`, 0 imported, 10 rejected — 4 bad dates plus 6 emails already in the database |
| 13 | `POST /employees/bulk` | no file, or a `.txt` file | `400` |

Rows are visible in the database directly:

```bash
docker compose exec postgres psql -U postgres -d employee_management \
  -c 'SELECT "Name", "Email", "Status", "HireDate" FROM "Employees";'
```

---

## Business rules

Required by the assignment:

1. **Email is unique.** Checked in the service (clear 409) and enforced by a unique index in the database (protects against two concurrent requests). Emails are trimmed and lower-cased on input, so `Jan@x.com` and `jan@x.com` are the same person.
2. **HireDate cannot be in the future.** "Today" is the current UTC date.
3. **PhoneNo must be E.164** (`+` and 2–15 digits). Spaces, hyphens, dots and parentheses are removed first, so `+1-555-0101` is stored as `+15550101`. This checks the format, not whether the number really exists.

Additional rules:

4. **Status must be `Active` or `Inactive` (case-insensitive).** Any other value is rejected with 400 and never silently replaced. When creating, a missing Status means `Active`; when updating, Status is required, otherwise a forgotten field would silently reactivate an inactive employee.
5. **HireDate cannot be more than 70 years in the past.** No realistic employment lasts longer; the rule catches typos such as `1899`. It is relative to today, so a very old record could eventually fail validation on update.
6. **Required fields (Name, Email, PhoneNo, HireDate) cannot be empty or whitespace**, and every text field has a maximum length that matches the database column, so an over-long value is a 400, not a database error.
7. **ProfilePicture, when given, must be an absolute `http`/`https` URL.** This rejects values such as `javascript:...`. The URL is only stored, never fetched by the server.
8. **Id and CreatedAt are set by the system only.** The request body has no such fields, and an update never changes them.

Postal codes (`Pincode`) get only a loose check (3–10 letters, digits, spaces or hyphens): formats differ too much between countries (`20-001`, `560001`, `SW1A 1AA`) for a per-country rule in this scope.

---

## Bulk import

`POST /employees/bulk` with `multipart/form-data`, a `.csv` of at most 5 MB / 10,000 rows.
The form field has to be named exactly `file`; a file sent under any other field name is rejected with 400.

- Required columns: `Name, HireDate, Email, PhoneNo, Status`; optional: `ProfilePicture, Address, State, Country, City, Pincode`. Header matching ignores case (the sample uses `Hiredate`). Unknown columns are ignored, including `CreatedAt` from the sample, because CreatedAt is system-set.
- Dates must be `yyyy-MM-dd`. Unlike `POST /employee` (where a new hire defaults to `Active`), an import carries existing data, so `Status` must be stated for every row: a file without the column is rejected, and a row with an empty or unknown status is rejected. Former employees in a file are never silently activated.
- **Every row goes through the same normalization and validator as `POST /employee`.** Duplicate emails are detected both within the file and against existing employees.
- **Partial success:** valid rows are saved, invalid rows are listed with their line number in the file (header = line 1) and the reasons. One bad row does not block the other rows. Rows are never "fixed" - e.g. the sample's `CreatedAt` column is not used to guess a correct HireDate.
- Valid rows are saved in a single `SaveChanges`. If a concurrent request inserted one of the emails in the meantime, the import falls back to row-by-row saving and reports only the conflicting rows.
- The whole file is rejected (400) only if it cannot be used at all: missing file, wrong extension, missing required columns, no data rows.

Result for the provided `samples/employees_sample.csv`: **6 imported, 4 rejected**:

| Line | Employee | HireDate | Reason |
|---|---|---|---|
| 3 | Sarah Johnson | 2079-07-01 | in the future |
| 8 | David Anderson | 2019-13-12 | not a valid date |
| 9 | Amanda Thomas | 1899-02-28 | more than 70 years ago |
| 11 | Jennifer Lee | 2081-08-22 | in the future |

---

## Design decisions

- **Controllers in a single project with folders** (not Minimal APIs, not Clean Architecture). The routes mix singular and plural paths, so each action declares its full route. Separate projects would be over-engineering for this scope.
- **PostgreSQL + EF Core.** The data is relational with a uniqueness constraint; EF Core gives parameterized queries and migrations. Docker Compose makes the database one command away.
- **No repository layer.** `DbContext`/`DbSet` already is the unit of work and repository; another wrapper would add code without benefit.
- **Guid ids generated in code.** Not guessable like sequential integers, and available immediately for the `Location` header.
- **`DateOnly` for HireDate.** It is a date, not a moment in time; no time-zone surprises.
- **Status stored as text.** Readable when querying the database directly.
- **One request DTO for POST and PUT, two validators.** The payload shape is identical (PUT is a full replace), only the Status rule differs. Both validators implement `IValidator<EmployeeRequestDto>`, so they are registered as keyed services.
- **Lenient request DTO.** All request fields are nullable so that a missing value reaches the validator (e.g. a missing HireDate is not silently `0001-01-01`) and all errors are reported together. Status is a string because JSON enum binding accepts any number.
- **Response DTO with `required` members.** The compiler guarantees that the mapping sets every field.
- **Results instead of exceptions for expected outcomes.** The service returns `ServiceResult` (validation / not found / conflict); only unexpected failures are exceptions, handled in one place by `GlobalExceptionHandler`.
- **`TimeProvider` instead of `DateTime.UtcNow`.** Date rules are testable with a fixed "today".
- **`EnableRetryOnFailure`.** Transient database errors are retried; when retries run out the client gets 503.
- **`CancellationToken` passed everywhere.** A disconnected client stops the database work.
- **A custom `DateOnly` JSON converter.** Dates are accepted only as `yyyy-MM-dd`, and an unparsable value produces a readable message instead of the default one, which exposes an internal .NET type name. Model binding runs before FluentValidation, so this is the only place such a message can be shaped.
- **Scalar instead of Swashbuckle.** It builds on the OpenAPI document that .NET 10 already generates.

## Security

- **SQL injection:** all database access is LINQ through EF Core, which sends values as parameters; there is no raw SQL. The only list filter (`emails.Contains(...)`) is also parameterized.
- **Input trust:** the client cannot set `Id` or `CreatedAt`; all input is normalized and validated; text lengths are limited; URLs are restricted to http/https; uploads are limited to 5 MB and 10,000 rows.
- **Errors:** responses never contain exception details or stack traces; details go to the logs.
- Not in scope: authentication/authorization and rate limiting.

## Testing

- **Validator tests:** every business rule, including edge cases (today, exactly 70 years, `Active,Inactive`, numeric status, `javascript:` URL, missing date reporting only one error).
- **Service tests (InMemory):** update logic (fields changed, Id/CreatedAt kept, own email allowed, conflict, not found, invalid data saves nothing, missing Status rejected), create defaults, and import (the provided file, duplicates within the file and against the database, unusable files).
- **API tests (`WebApplicationFactory`):** create + get, 400 with all field errors, 409, 404, delete, bulk import.

Limitation: the InMemory provider does not enforce the unique index, so the concurrent-duplicate path (database unique violation -> 409) is not covered by automated tests.

## With more time

- Pagination, filtering and search for `GET /employees` (the sample UI has them).
- Integration tests against a real PostgreSQL (Testcontainers), covering the unique-index race.
- Per-country postal code validation and real phone number validation (libphonenumber).
- Streaming the import for very large files and processing it in the background with a status endpoint.
- Authentication, rate limiting, structured logging, health checks, CI.
- Running the API itself in Docker Compose. Compose currently provides only the database and the API runs on the host, which is what the assignment asks for. Containerising the API as well would need a multi-stage Dockerfile, an `api` service with `depends_on: condition: service_healthy`, a connection string pointing at the `postgres` service instead of `localhost`, and a decision on how migrations run inside the container: the ASP.NET runtime image has no `dotnet-ef`, so it would be either an EF migration bundle as a one-shot service or `Database.Migrate()` behind a configuration flag.
- Soft delete and optimistic concurrency (row version) instead of last-write-wins updates.

## AI tool usage

I used **Claude** as a pair programmer: planning the architecture, reviewing my code, explaining trade-offs and, under time pressure, writing a large part of the code. I reviewed every change, asked for the reasoning and made the final decisions.

Examples of suggestions I changed or rejected:

- It proposed three DTOs (create, update, response). I questioned it: create and update have the same shape, so they share one DTO.
- It proposed a minimum hire date of 1900. I rejected the arbitrary year and chose a rule based on career length: at most 70 years ago, relative to today.
- It registered the two validators by concrete class. I asked about depending on interfaces, which led to keyed services with `IValidator<T>`.
- It proposed a positional record for the response. My question about it revealed the risk of swapping same-typed arguments, so the response uses `required` properties.
- I decided that a new employee is `Active` by default, that emails are stored in lower case, and to postpone Docker until the code needed a real database.
- It gave a `dotnet sln EmployeeManagement.sln` command that failed because .NET 10 creates `.slnx` files; I caught it when running it.
