# UI overhaul validation

The desktop remains Vue 3/Vite in WebView2 with the existing C# Core and Infrastructure services. All interface icons are imported from the official @lucide/vue package (https://lucide.dev/icons/). Branding uses a copy of the existing logo.png asset.

## Changes

- One page header with player count and aligned category controls; responsive filter menu.
- 174px sidebar, 46px navigation, pink selection marker, small connection/version footer.
- Default native window 1180 x 740 with a 40px title bar.
- 38px search, 36px row avatars, 54px normal rows, separators and selected-row styling.
- 24px outlined category labels and one icon per visible category in compact mode.
- 285px profile panel, full-width list when closed, overlay drawer at narrow widths.
- Real user-ID copy/open-profile actions routed through validated native commands.
- Quiet empty state and matching Home/Recent/Settings surfaces.
- No permanent bottom status bar, green tag switches, gradients or glow effects.

## Development preview

From the repository directory:

    dotnet build src/ELIXR.PlayerList.Desktop -c Debug -p:UiPreview=true
    .\src\ELIXR.PlayerList.Desktop\bin\Debug\net8.0-windows\ELIXR.PlayerList.exe --ui-preview

The 18 fictional entries are marked Demo, use preview_fictional IDs, and have local illustrated SVG portraits. Preview is a distinct Vite build mode enabled by the native Debug-only flag. It does not start the log watcher, send settings commands or connect fixture data to services. Production mode excludes the fixture module; Release ignores the flag.

Automatic capture, using an absolute existing/writable output directory:

    ELIXR.PlayerList.exe --ui-preview --capture-ui "C:\Temp\elixr-ui-captures"

UiCapture.cs runs inside the actual Debug WebView2 host. It composites the native window render and WebView2 CapturePreview image, yielding 1180 x 740 captures for default-size states. Narrow captures use 760 x 740. It checks dimensions and interactions and writes checks.json.

## Results

- Release solution build: passed, zero warnings/errors.
- Release publish: passed.
- Existing C# tests: 4 passed.
- Frontend tag tests: 4 passed.
- Native production smoke: 8 passed, including settings save acknowledgements with isolated settings.
- Preview UI checks: 19 passed.
- Fixture scan of production assets: no fictional preview IDs, sample tag labels or preview fixture chunk.
- Captures inspected after multiple styling/capture passes: full closed/open, compact closed/open, empty, narrow drawer/menu, long names and many tags.
- Exact measured sidebar 174px, normal row 54px, profile panel 285px.

Captures are in artifacts/ui-overhaul:
01-full-tags.png, 02-full-profile.png, 03-compact-tags.png, 04-compact-profile.png, 05-empty.png, 06-narrow-profile.png, 07-long-name-many-tags.png, 08-narrow-filters.png.

## Modified files

Frontend:
- src/App.vue
- src/style.css
- src/state.js
- src/components/AppIcon.vue
- src/components/ToggleSwitch.vue
- src/components/PlayerTable.vue
- src/components/TagControls.vue (new)
- src/components/PlayerAvatar.vue (new)
- src/components/ProfilePanel.vue (new)
- src/preview.js (new)
- src/assets/elixr-logo.png (copied existing brand asset)

Native desktop:
- MainWindow.xaml (window dimensions/title styling)
- MainWindow.xaml.cs (preview guard, validated profile actions, updated smoke selectors)
- App.xaml.cs (separate preview settings)
- ELIXR.PlayerList.Desktop.csproj (Debug-only preview build selection)
- UiCapture.cs (new, Debug-only visual/interaction diagnostic)

Documentation:
- README.md
- docs/UI_OVERHAUL.md

Core and Infrastructure source files are unchanged.

## Remaining differences and integration limits

The available brand asset is a potion, unlike the angular pink logo in the supplied reference. Preview avatars are locally authored illustrations, not the reference's anime images. No online dots, presence labels, platform data, avatar banners, or unimplemented chat/more actions are fabricated. These account for visible differences from the reference.

The real log watcher is preserved and parser tests pass; a live VRChat session was not used for this visual work. Backend tag fetching and SteamVR rendering were not implemented in the starting project, so they cannot be verified as working. The tag display accepts existing model data and uses shared persisted visibility preferences; an eventual native overlay must consume the same settings through the preserved service boundary.

