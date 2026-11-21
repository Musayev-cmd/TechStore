# TechStore

Full-stack e-commerce platform for electronics, laptops, PC components, monitors, storage, and peripherals built with **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core** (SQLite locally, PostgreSQL-ready for production), **ASP.NET Core Identity** (`Admin` / `Customer` roles), **Stripe Checkout + verified idempotent webhooks**, **trilingual localization (`EN` / `RU` / `AZ`)** with **AZN (`₼`)** pricing, and a **vanilla CSS3 + ES6+ JS** frontend (Matte Graphite dark/light themes, zero Bootstrap/Tailwind/jQuery).

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`net10.0`)
- Optional: `dotnet-ef` CLI tool (`dotnet tool install --global dotnet-ef`) for managing EF Core migrations
- Optional: [Stripe CLI](https://docs.stripe.com/stripe-cli) for local webhook forwarding
- Optional: Docker Engine + Docker Compose v2 for containerized PostgreSQL deployment

---

## Local Setup (.NET 10 + SQLite)

TechStore runs out of the box with SQLite (`Data/techstore.db`) and requires zero external services to browse, filter, manage the cart, or test the Admin Panel.

```bash
# 1. Restore dependencies and verify clean build
dotnet build

# 2. (Optional) Apply EF Core migrations explicitly — Development also auto-migrates on boot
dotnet ef database update

# 3. Start the application
dotnet run
```

- **Storefront (HTTP):** `http://localhost:5080`
- **Storefront (HTTPS profile):** `dotnet run --launch-profile https` → `https://localhost:7080`
- **Admin Panel:** `http://localhost:5080/Admin`

On first start, `DbSeeder` idempotently seeds roles (`Admin`, `Customer`), the Admin account, 6 hardware categories, and 16 trilingual products with local SVG artwork.

---

## Dev Admin Credentials & `dotnet user-secrets`

No passwords or API keys are hardcoded in source code or `appsettings.json`.

### Automatic Dev Bootstrap
In `Development`, if `SeedAdmin:Password` is not configured when the Admin account is first created, `DbSeeder` generates a cryptographically strong 16-character password and logs a startup banner to the console:

- **Default Email:** `admin@techstore.local`
- **Password:** printed once in the startup console output

### Configuring Fixed Local Secrets (`dotnet user-secrets`)
`TechStore.csproj` is preconfigured with `UserSecretsId` (`techstore-ecommerce-dev-secrets`). Set a fixed admin password and Stripe test credentials locally without touching `appsettings.json`:

```bash
dotnet user-secrets set "SeedAdmin:Email" "admin@techstore.local"
dotnet user-secrets set "SeedAdmin:Password" "<YourStrongPassword>"

dotnet user-secrets set "Stripe:SecretKey" "<YourStripeTestSecretKey>"
dotnet user-secrets set "Stripe:PublishableKey" "<YourStripeTestPublishableKey>"
dotnet user-secrets set "Stripe:WebhookSecret" "<YourStripeWebhookSigningSecret>"
```

> **Password policy:** minimum 8 characters with at least one uppercase letter, lowercase letter, digit, and non-alphanumeric symbol.
> If the default Admin account was already seeded with a generated password and you want to re-seed with your custom password, delete `Data/techstore.db` before restarting, or sign in and change it at `/Account/Profile`.

---

## Local Stripe Webhook Testing (Stripe CLI)

Order fulfillment is authoritative and idempotent via `POST /webhooks/stripe` (`StripeWebhookController` + `StripeCheckoutService`):
- Verifies the `Stripe-Signature` HMAC header via `EventUtility.ConstructEvent`.
- Deduplicates deliveries through the `ProcessedStripeEvents` table.
- Atomically decrements `Product.StockQuantity` inside a DB transaction, marks the order `Paid`, and clears the buyer's cart.

To test Checkout and webhook fulfillment locally:

1. Configure your Stripe test API keys using `dotnet user-secrets` (see above).
2. Authenticate the Stripe CLI and forward events to your local TechStore instance:
   ```bash
   stripe login
   stripe listen --forward-to http://localhost:5080/webhooks/stripe
   ```
3. Copy the webhook signing secret printed by `stripe listen` and save it in user-secrets:
   ```bash
   dotnet user-secrets set "Stripe:WebhookSecret" "<WebhookSigningSecret>"
   ```
4. Start (or restart) the app with `dotnet run`.
5. Complete a Checkout session in the browser (using Stripe test cards) or trigger a test delivery:
   ```bash
   stripe trigger checkout.session.completed
   ```
6. Confirm the order transitions to `Paid` on `/Checkout/Success`, `/Account/Orders`, and `/Admin/Orders`.

---

## Switching to PostgreSQL (`Database:Provider`)

Switch from SQLite to PostgreSQL with configuration alone (`Database:Provider` accepts `PostgreSQL` or `Postgres`, case-insensitive):

```bash
# Example via environment variables (Linux/macOS)
export Database__Provider="PostgreSQL"
export Database__AutoMigrate="true"
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=techstore;Username=techstore;Password=<YourDbPassword>"
```

```powershell
# Example via PowerShell
$env:Database__Provider = "PostgreSQL"
$env:Database__AutoMigrate = "true"
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=techstore;Username=techstore;Password=<YourDbPassword>"
```

Or persist locally via `dotnet user-secrets`:

```bash
dotnet user-secrets set "Database:Provider" "PostgreSQL"
dotnet user-secrets set "Database:AutoMigrate" "true"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=techstore;Username=techstore;Password=<YourDbPassword>"
```

**Schema notes:**
- `AppDbContext` stores all timestamps in UTC (`DateTime.UtcNow`) and keeps native `numeric(18,2)` columns on PostgreSQL (the `decimal -> double` converter applies exclusively to SQLite).
- The bundled `Data/Migrations/` snapshot targets SQLite for zero-config local setup. When `Database:Provider` is `PostgreSQL` and `Database:AutoMigrate` is `true`, `DbSeeder` automatically detects this and bootstraps the native PostgreSQL schema (`EnsureCreatedAsync`) idempotently on first boot.
- For teams managing incremental PostgreSQL schema changes with EF Core Migrations, regenerate migrations with `Database__Provider=PostgreSQL` configured (`dotnet ef migrations add InitialCreate`).

---

## Docker Deployment

The repository includes a multi-stage `Dockerfile` (`.NET 10` SDK build → non-root `aspnet:10.0` runtime), `.dockerignore`, `docker-compose.yml` (`app` + `postgres:17-alpine` with healthcheck), and `.env.example`.

1. **Create your `.env` file** (excluded from Git via `.gitignore`):
   ```bash
   cp .env.example .env
   ```
   Set strong, unique values in `.env` for:
   - `POSTGRES_PASSWORD` (required — Compose fails fast if unset)
   - `SEED_ADMIN_PASSWORD` (required on first start in `Production` to create the Admin user)
   - `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY`, `STRIPE_WEBHOOK_SECRET` (required for payments)

2. **Validate configuration and launch the stack:**
   ```bash
   docker compose config
   docker compose up -d --build
   ```

3. **Access TechStore:**
   - Storefront: `http://localhost:8080` (override host port via `APP_PORT` in `.env`)
   - Admin Panel: `http://localhost:8080/Admin`

4. **Persistent Docker Volumes:**
   - `techstore_pgdata` → `/var/lib/postgresql/data` (PostgreSQL database storage)
   - `techstore_uploads` → `/app/wwwroot/uploads/products` (Admin-uploaded product images)
   - `techstore_dataprotection` → `/home/app/.aspnet/DataProtection-Keys` (ASP.NET Core auth/CSRF encryption keys)

5. **Operational commands:**
   ```bash
   docker compose logs -f app
   docker compose down          # Stop containers, keep data & upload volumes
   docker compose down -v       # Stop containers and remove all volumes
   ```

---

## Configuration Reference

| Key (JSON / User-Secrets) | Environment Variable | Default | Purpose |
|---|---|---|---|
| `Database:Provider` | `Database__Provider` | `Sqlite` | Database engine (`Sqlite` or `PostgreSQL`) |
| `Database:AutoMigrate` | `Database__AutoMigrate` | `false` | Apply schema/migrations automatically on startup (`Development` always enables it) |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | `Data Source=Data/techstore.db` | SQLite file path or Npgsql connection string |
| `SeedAdmin:Email` | `SeedAdmin__Email` | `admin@techstore.local` | Initial Admin email address |
| `SeedAdmin:FullName` | `SeedAdmin__FullName` | `TechStore Administrator` | Initial Admin display name |
| `SeedAdmin:Password` | `SeedAdmin__Password` | *(empty)* | Initial Admin password (auto-generated in Dev if empty; required in Production) |
| `Stripe:SecretKey` | `Stripe__SecretKey` | *(empty)* | Stripe API secret key |
| `Stripe:PublishableKey` | `Stripe__PublishableKey` | *(empty)* | Stripe publishable key |
| `Stripe:WebhookSecret` | `Stripe__WebhookSecret` | *(empty)* | Stripe webhook endpoint signing secret |
| `Store:CurrencyCode` | `Store__CurrencyCode` | `AZN` | Store currency code |
| `Store:FreeShippingThreshold` | `Store__FreeShippingThreshold` | `250.00` | Subtotal threshold for free shipping |
| `Store:FlatShippingFee` | `Store__FlatShippingFee` | `10.00` | Flat shipping fee below threshold |
| `Store:LowStockThreshold` | `Store__LowStockThreshold` | `5` | Low-stock alert threshold |
