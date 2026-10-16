# Vue and WebView2 migration

## Inspection and plan

Preserve the existing Core and Infrastructure projects: player/instance/tag models, tag rules, VRChat log parser, live log service, atomic JSON settings, and service contracts. Replace the desktop XAML UI, view models and custom icon renderer with Vue components. Keep WPF only as a thin native window around WebView2.

Sequence implemented:

1. Add Vue/Vite and components for navigation, switches, category badges and the player table.
2. Bundle built assets locally and implement an origin-checked C# JSON bridge.
3. Connect the existing live player service and settings store.
4. Match the approved reference's narrow sidebar, cyan selection edge, category colors/icons, charcoal surfaces, compact rows and thin separators.
5. Verify category modes, native navigation, settings acknowledgements, publish output and Windows startup.

## Ownership

| Component | Owner |
| --- | --- |
| Navigation, layouts, search, profiles, category display | Vue |
| Native window and WebView2 lifetime | Desktop host |
| Models and tag/auth/overlay contracts | Core, unchanged |
| VRChat log activity and settings | Infrastructure, unchanged |
| Future API credentials, registration and updates | C# services |
| Future SteamVR rendering/controller interaction | Separate native overlay implementation |

There was no implemented SteamVR overlay in this project: IOverlayService is a contract only. The host accepts an optional implementation and reports the overlay unavailable when none is provided. The adjacent Python overlay is unchanged. A desktop UI reload does not recreate native services.

## Bridge protocol

The host accepts only messages from https://elixr.example, a reserved origin mapped to bundled local files. External navigation, popups, permissions and downloads are blocked; host objects are disabled. Messages have a 4 KB limit and explicit allowlist.

- UI to native: ready; settings.set with key/value; overlay.visibility with visible.
- Native to UI: state with payload; generic error with message.
- State includes players, instance identity, settings and capability flags. No credentials are exposed.
- Settings accept supported boolean keys and bounded numeric scale/opacity; writes use the existing atomic JSON store. Failed saves restore in-memory values and report an error.
- Native service changes push state to Vue. There is no browser polling loop, animation loop, telemetry or HTTP listener.

## Remaining integrations

The player reader now reconstructs the latest instance from the existing log on startup, then streams newly appended bytes. It handles split UTF-8 writes, log rotation/truncation, delayed log directories, process exit/restart, partial identities and duplicate room markers. Authentication and native FewTags/ETags/group-tag retrieval have since been implemented. A nonblocking player-service decorator enriches the local roster without changing the working log reader. Public profiles, recent history, secure updates and SteamVR rendering remain separate work. See MOTION_AND_LOGIN.md and README.md. Synthetic player data remains isolated to development preview and tests.

## Verification

Use npm test in frontend/, dotnet test, and the opt-in --smoke-test diagnostic. The latter exercises all four views, native state and settings save acknowledgements with isolated settings, captures the actual packaged WebView and exits.

Sources: [WebView2 security](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security), [local content](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content).

