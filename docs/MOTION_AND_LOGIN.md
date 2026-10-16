# Approved UI, motion and sign-in implementation

## Design and motion

The standard Windows window frame replaces custom XAML title controls. DwmSetWindowAttribute requests the dark system title bar. Vue remains hosted locally in WebView2; no Electron, HTTP listener or architecture change was introduced.

The new theme uses neutral #111111/#151515/#171717 surfaces, restrained borders, inset navigation selection with a small pink marker, and a version-only sidebar footer. The original potion logo remains. Mockup anime portraits are not bundled as real player images.

Motion uses motion-v physical springs for shared navigation selection, row entry/reflow/exit, tag transitions, toggle thumbs, press feedback, filter popups and the profile slide. Page and portrait entrances use coordinated transform animations. The short bouncing loader exists only during session restoration. Source-requested tag text effects use bounded CSS animations, paused offscreen or when the document is hidden. CSS and the motion configuration honor prefers-reduced-motion.

## Authentication

The native VrchatAuthenticationService implements the existing IAuthenticationService contract with username/password login, TOTP/email OTP/recovery-code verification, current-user validation, session restoration, cooldown after HTTP 429 and logout. Only the verified public account DTO, status and method names cross back to Vue.

Passwords are individually URL-encoded for the Basic auth request and sent to the fixed https://api.vrchat.cloud/api/1/auth/user endpoint. Automatic redirects are disabled. This follows the existing ELIXR flow and the community-maintained VRChat API specification; it is not a claim that VRChat offers a formally supported OAuth flow.

Remember me encrypts only auth and twoFactorAuth cookies with Windows DPAPI CurrentUser under %LOCALAPPDATA%/ELIXR/PlayerList/session.bin. Cookies include expiry, and rejected sessions are deleted. No password is stored. Login form values are cleared after submission; WebView2 autosave/autofill are disabled. Offline restoration preserves saved tokens for explicit retry. Signing out clears local state and attempts server logout.

The log watcher starts after a verified sign-in. Native bridge commands require authentication (development preview is compile-time isolated). Native tag fetching now uses the three original public GitHub feeds, with a separate cookie-free HTTP client, local fallback caches and authenticated VRChat public-membership matching for group tags. The overlay retains its existing boundary; native SteamVR rendering still needs implementation.

Player detection now replays existing client-log events to reconstruct the current instance before publishing a single roster update, then reads only appended bytes. A 750ms recovery timer handles log rotation, incomplete writes, missing log directories and VRChat process restarts without depending on filesystem notifications. Unchanged logs do not publish UI updates. Nine log-service regression tests cover these cases, and the reader was checked against an active local VRChat log without exporting player identities.

## Validation and screenshots

Authentication tests use injected HTTP responses rather than real credentials. They cover encoded credentials, fixed destination, remembered-cookie reuse, Remember me off, verification methods, post-verification user validation, expired sessions, offline restoration, throttling and logout. A Windows test exercises actual DPAPI encryption/decryption, on-disk secrecy and corrupted-file removal.

Debug UI diagnostics cover populated/full/compact lists, profile opening/closing, spring movement and settling, search, category filtering, narrow filters/drawer, all navigation sections, login fields, password visibility, verification methods and reduced-motion loader behavior. Native frame screenshots include the real Windows title bar.

Latest captures are under artifacts/approved-motion. Production startup is tested with --smoke-test and isolated credential storage. No real VRChat account was used for automated tests, so live sign-in and server-specific challenges still require user testing. Frontend preview fixtures are excluded from production builds.

## Changed areas

- frontend/src/theme.css, motion.js and existing App/table/tag/switch/filter components.
- New frontend/src/components/LoginScreen.vue, native bridge state and imports.
- Native MainWindow XAML, dark-frame initialization, authentication bridge and startup gating.
- New Infrastructure VrchatAuthenticationService and WindowsSessionStore; expanded authentication interface.
- AuthenticationTests and SessionProtectionTests; extended Debug-only UI capture diagnostics.
- README, PRIVACY, gitignore and package dependency lockfiles.

References: [VRChat login specification](https://vrchat.community/reference/get-current-user), [verification](https://vrchat.community/reference/verify2fa), [Motion for Vue](https://motion.dev/docs/vue-animation), [Windows dark frame](https://learn.microsoft.com/windows/apps/desktop/modernize/apply-windows-themes).

