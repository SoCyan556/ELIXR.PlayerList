# Contributing

Desktop presentation lives in frontend/src (Vue 3/CSS). Keep native API access in C# and extend the bridge through an explicit command allowlist. Run npm test in frontend/ in addition to the .NET checks. Commit package-lock.json for dependency changes.

Use .NET 8, keep nullable analysis and warnings-as-errors enabled, and preserve the core/infrastructure/UI boundaries. New integrations require tests and must degrade gracefully when VRChat, SteamVR, ELIXR, or GitHub is unavailable.

Do not add telemetry, secrets, real user data, generated databases, or permanent mock player data. Run `dotnet build` and `dotnet test` before opening a change.
