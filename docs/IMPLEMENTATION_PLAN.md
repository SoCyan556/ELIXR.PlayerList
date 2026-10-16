# Implementation plan

1. Foundation — complete: solution structure, domain models, contracts, settings, live log tail, desktop shell, baseline tests.
2. Desktop UI — migrated to Vue 3 + Vite + WebView2. Four views, search, full/compact tag badges, basic profile dialog and persisted settings are implemented. Profile hydration and recent data still require native service implementation. See FRONTEND_MIGRATION.md.
3. VRChat integration — port audited auth state machine, protect cookies with Windows credentials, hydrate public profiles within rate limits.
4. Tag integration — define authenticated ELIXR API DTOs, enforce server permissions, cache transient failures, implement all three categories.
5. VR overlay — isolate OpenVR interop in a dedicated process/library, render only on state changes, add controller-safe scrolling and scale/opacity controls.
6. Recent players/settings — local SQLite history, retention/clear controls, robust migrations, persistent preferences.
7. Registration/privacy — server verifies VRChat identity and idempotently stores only the user ID; add authorized aggregate/list endpoints separately.
8. Updates — GitHub stable release metadata, signed manifest/hash validation, staged replacement, rollback-safe updater.
9. Packaging — integration tests, signed MSIX/installer, release checklist, dependency/security audit.

Each network-dependent phase remains blocked on the concrete endpoint schemas, repository identity, and signing policy listed in the README.
