# ELIXR Player List

A standalone Windows VRChat companion. The desktop frontend uses Vue 3 + Vite in Microsoft WebView2, hosted by a small .NET 8/WPF window. C# owns the backend services. There is no Electron, localhost server, or dependency on the adjacent Python VRTool.

## Download

Get the Windows x64 installer or portable ZIP from [GitHub Releases](https://github.com/SoCyan556/ELIXR.PlayerList/releases/latest). Windows 10 2004 or newer / Windows 11 is required. The installer runs per-user without administrator rights and bundles .NET, all application/frontend files, and Microsoft's complete x64 WebView2 Evergreen offline installer. No internet or separate package download is needed to install the app or its prerequisite. Internet is still needed for VRChat login, community feeds and updates. The portable ZIP needs an existing WebView2 Runtime; use the full installer if unsure. Neither needs Node.js or a separate .NET installation.

ELIXR builds are currently **unsigned**: Windows may display an unknown-publisher/SmartScreen warning. Download only from the official repository and verify SHA256SUMS.txt if needed. The first public release is an early version; known limitations are listed below.

## Status

Starting with 0.1.2, the tool registers your own VRChat ID once after successful sign-in so ELIXR's private admin panel can count lifetime unique Player List users. No passwords, session cookies, player rosters, or ongoing activity are sent to ELIXR. See [privacy details](PRIVACY.md). Earlier usage cannot be reconstructed; this client-reported metric is not proof of account ownership.

The reference-driven UI overhaul, exact preview/capture commands, changed files, screenshots and verification results are documented in [UI_OVERHAUL.md](docs/UI_OVERHAUL.md). Development preview is available only through a Debug build with UiPreview=true and the --ui-preview flag.

Implemented: native Windows dark title bar, neutral charcoal UI, inset spring-animated navigation, four sections, player table/search/profile inspector, full/compact tag badges, persistent settings, and VRChat login with verification, Windows-encrypted remembered sessions, startup restoration and logout. See [motion and login notes](docs/MOTION_AND_LOGIN.md).

Profile details beyond portraits, local encounter history, and native SteamVR rendering remain future integrations. Production shows only real player activity after authentication. Development-only fictional data is isolated from backend services. The overlay interface remains separate from the desktop WebView.

Startup checks the official repository's latest stable GitHub release (configurable in Settings). A newer version with a valid installer asset opens an animated **Update now / Maybe later** dialog, including at the sign-in screen. Maybe later dismisses it for this launch; Settings → Check for updates checks again. Update now downloads the installer through a cookie-free native client, validates its exact size and GitHub-provided SHA-256 digest, then opens the normal interactive installer and closes ELIXR. No executable is run if validation fails. These hashes validate download integrity, not an independent code-signing identity. Settings, encrypted sessions and caches are preserved during upgrades and uninstall; they remain in LocalAppData/ELIXR/PlayerList until the user removes them.

Player portraits load from authenticated VRChat user profiles in a background pipeline independent of log reading and tags. Preference is the user's icon, profile-picture override, then avatar thumbnail. The native service requests small thumbnails, throttles profile requests, caches profile image selection for ten minutes, honors rate limits, and keeps a bounded 32 MiB raster-image cache in `%LOCALAPPDATA%/ELIXR/PlayerList/PlayerImages`. Metadata and images remain native: browser components receive opaque local-cache URLs only. API redirects are checked at every hop; CDN requests use a cookie-free client. Main list, profile panel portrait, and desktop overlay share these images. Missing/failed/private images retain the person icon. Initial images appear progressively rather than holding up the roster.

Tags use the original ELIXR sources: [FewTags](https://raw.githubusercontent.com/Fewdys/FewTags/refs/heads/main/FewTags.json), [ETags](https://raw.githubusercontent.com/SoCyan556/ETags/refs/heads/main/ETags.json), and [etags-groups](https://raw.githubusercontent.com/SoCyan556/etags-groups/refs/heads/main/groups.json). The native service downloads these public catalogs every ten minutes (one-minute retry on failure), retains last-good caches, respects active flags and priorities, and gives ETags precedence over duplicate FewTags labels. Group labels match the player's public group IDs from VRChat, not their display name. Membership requests are single-flight, throttled, cached in memory for ten minutes, and honor rate-limit cooldowns. Private/hidden memberships cannot be matched.

Full labels preserve source colors, safe rich-text color runs, gradients, rainbow, pulse, blink, glitch and shake styles. FewTags enlarged/heavy styles retain the original compatibility layer's static fallback. Text size remains compact for this desktop layout. Animations pause offscreen/when hidden and honor Windows reduced-motion preferences. Compact mode retains red skull, pink smile and blue group indicators. Source-provided community labels are not verified claims or application permissions. Feed status and the AI assistance statement appear in Settings.

The monitor button in the header and **Settings → Desktop overlay → Always-on-top player list** toggle a separate native topmost window. It shares the existing roster, tag filters and compact-tag setting, and stays visible when the main window is minimized. Its reference-inspired design uses a rounded dark plum shell, pink ELIXR mark/selection accents, inset rows, portrait presence dots (current instance, not a guessed global status), and pink scrollbar. Drag the ELIXR header to move (native WebView2 non-client drag regions, not asynchronous mouse forwarding). The diagonal-arrow **Resize overlay** button opens a temporary native editor: the desktop dims around the live overlay, pink edge/corner handles and a move grip adjust its bounds, and width/height sliders provide a live preview. Save layout persists size/position atomically; Cancel/Escape restores the original bounds. Editor entry, handle pop-in and exit animations honor Windows animation preferences. Ordinary drag/resize changes are also saved after a short debounce and on close. Layout is restored after app restarts and clamped to the available desktop after monitor changes; overlay visibility remains off on startup. The search icon reveals/focuses search. The settings gear contains tag filters and a compact-layout toggle (270px / 440px at normal scale); narrow layouts automatically use category icons. Closing the overlay updates both toggles; closing ELIXR or signing out closes it too. Its WebView is created only when opened and disposed when closed; it shares the existing WebView2 environment instead of duplicating backend services. The main window keeps its native Windows title bar. This is independent of SteamVR. Windows secure desktop and exclusive-fullscreen applications may appear above ordinary topmost desktop windows. The overlay is opaque, not a desktop-blur/transparency effect.

Click an overlay player to expand their actions (one row at a time). **Copy user ID** uses the Windows clipboard and confirms success after the native copy. **Open in app** restores/focuses the main ELIXR window, navigates to Players and opens that player's existing profile panel; it does not launch a web browser or VRChat. Actions remain disabled while identity is pending. Click again or press Escape to collapse. Players who leave are removed with their dropdown, and native profile selection checks that the player still belongs to the current roster.

Group tags in the in-app profile include the actual group name and full VRChat group code (for example `ELIXR.0042`) with a copy button. Metadata comes from the existing public-membership lookup, with feed group names as fallback; no extra group requests are needed. Codes retain discriminator leading zeros. Unknown codes are shown as unavailable and cannot be copied. Native copy resolves the code from the current roster/group identity rather than trusting text supplied by the frontend. Identical tag labels from different groups remain separate.

See [migration notes](docs/FRONTEND_MIGRATION.md) and [implementation plan](docs/IMPLEMENTATION_PLAN.md).

## Build and run

Use Windows 10/11, .NET SDK 10 (tested; output targets .NET 8), Node.js 22.12+ (Node 24 tested), npm, and Microsoft Edge WebView2 Evergreen Runtime. A newer SDK is needed for the SLNX solution format. With SDK 8, build/test the individual project paths directly.

    dotnet build ELIXR.PlayerList.slnx -c Release
    dotnet test ELIXR.PlayerList.slnx -c Release
    dotnet run --project src/ELIXR.PlayerList.Desktop
    dotnet publish src/ELIXR.PlayerList.Desktop -c Release -o artifacts/desktop --no-self-contained

The build installs locked frontend dependencies on first use and builds Vite automatically. Build and publish outputs include the bundled frontend under wwwroot. Distribute the whole published folder. The release script publishes self-contained .NET and embeds the full offline WebView2 installer. A manual framework-dependent `dotnet publish --no-self-contained` build still needs a separately installed .NET 8 Desktop Runtime and WebView2; do not distribute that build as the complete installer.

For frontend development, run npm ci then npm run dev in frontend/. Browser preview is marked and has no native services. Build the desktop project to exercise the production bridge. Use npm ci after package-lock.json changes.

## Verification

Run npm test in frontend/ for category rendering rules, plus the existing .NET tests.

For a real WebView2 integration check, launch the executable with --smoke-test followed by an absolute JSON output path in an existing writable directory. It checks the startup sign-in screen with isolated settings/session storage, captures the WebView as a PNG, and exits. Debug --ui-preview --capture-ui checks all four sections and captures populated and authentication presentation states without contacting VRChat.

Debug-only --diagnose-images followed by an absolute JSON output path checks up to three other players from the current local roster using the remembered session. It reports response statuses, field names, image hosts and resolution success, never credentials, user IDs, names or signed image URLs. Portrait resolution supports both public-profile iconUrl and legacy account/avatar image fields.

## Configuration and privacy

Settings remain at %LOCALAPPDATA%/ELIXR/PlayerList/settings.json. Remembered cookies are stored in session.bin protected by Windows DPAPI for the current user. WebView2 runtime data stays in that directory's WebView2 subfolder. The appsettings.example.json file documents future backend configuration; VRChat authentication uses a fixed HTTPS API endpoint in the native service.

If startup fails, `startup-error.json` in that same folder records only the startup stage, error type/code, runtime/app versions, architecture and time. It is overwritten on the next failure and contains no credentials, browser state or profile data. A runtime disconnect is reported separately from a genuinely missing runtime or incomplete application files.

Required for future integrations: production ELIXR endpoints and schemas, permission claims, a server-side verified VRChat identity contract, GitHub repository/asset identity, signing policy, and a support address for VRChat's User-Agent.

Never include service-role keys, auth cookies, passwords, private signing keys, or user databases in the source or frontend bundle. See [privacy design](PRIVACY.md).

## Existing ELIXR reuse

The Core and Infrastructure projects are preserved. The adjacent Python code remains untouched. Its log parsing and 2FA authentication flow are useful porting references; its PyQt overlay is a desktop overlay, not a native SteamVR implementation. Telemetry, fingerprints, device identity and unrelated tools remain outside this project's scope.

## Releases and source packages

See [RELEASING.md](docs/RELEASING.md) for installer, portable ZIP, source ZIP, dependency notices and GitHub publication steps. `scripts/Build-Release.ps1` builds the self-contained Windows x64 distribution with Inno Setup; `scripts/Package-Source.ps1` packages only committed Git source. Binary packages do not contain credentials, caches, user logs or development preview fixtures. The source package includes the test/preview code needed to develop the app.

## License

[MIT](LICENSE), copyright 2026 SoCyan556 and contributors. Third-party software retains its own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). The license covers this standalone project, not the adjacent Python VRTool or external tag catalogs. This project is not affiliated with VRChat.

