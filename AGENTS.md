# Project Rules

## Tech Stack
- ASP.NET Core MVC (.NET 8+), Entity Framework Core, ASP.NET Core Identity.
- SQLite locally, PostgreSQL-ready configuration via appsettings/env vars.
- Vanilla CSS3 (CSS custom properties, Grid/Flexbox) and Vanilla ES6+ JS only. NEVER install or use Bootstrap, Tailwind, or jQuery.

## Quality & Verification
- Never hardcode Stripe keys, passwords, or secrets in code or appsettings.json.
- Always run `dotnet build` after finishing each milestone and fix all compiler warnings/errors before moving on.
- Keep RU, AZ, and EN localization keys 100% in sync (no missing keys or hardcoded UI strings).
- Do not leave TODO placeholders or stub methods in production code.
