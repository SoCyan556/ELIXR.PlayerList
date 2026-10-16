# Privacy design

The optional startup/manual update check contacts the public GitHub releases
API for SoCyan556/ELIXR.PlayerList. GitHub receives ordinary network metadata
(such as IP address), never VRChat credentials, account IDs or player lists.
Only clicking Update now downloads and launches an installer, after native
size/SHA-256 verification. Download files are held under LocalAppData/ELIXR/
PlayerList/Updates. Maybe later dismisses the prompt without installing.

The Vue frontend is bundled locally and hosted in WebView2, not loaded from an external website. The login form passes the submitted password or verification code once to the native service and clears the form value. Native session cookies are never sent back to JavaScript. WebView2 password autosave and general autofill are disabled.

The optional always-on-top desktop overlay displays the same local roster and settings in a second bundled WebView, using the existing browser environment. It does not start another log reader or issue additional backend requests. Its restricted bridge accepts readiness, close, header dragging, fixed compact/expanded sizing, tag-display filters, copying a validated user ID, and selecting a current roster player's profile in the main ELIXR window. It cannot submit login credentials or launch external URLs. Signing out or closing the main app closes the overlay; its visibility is not restored automatically on startup. The overlay is a visible desktop window: take care when sharing your screen.

Overlay width, height and desktop coordinates are saved locally in settings.json and are never transmitted. The resize editor is a temporary native dimming window: it does not capture, record, or send the contents of your desktop. Save commits the layout; Cancel/Escape restores its previous bounds. Ordinary moves and size changes are saved after a short debounce and when the overlay closes. Restored bounds are clamped to the currently available desktop if monitor geometry changes.

ELIXR Player List reads local VRChat client logs to determine the current instance roster. Recent-player history and settings are stored only on the user's computer.

While signed in, the native image service requests public profile image fields for encountered player IDs from VRChat. It does not store the full user-profile response or send these IDs to ELIXR/GitHub. Only raster images are cached on disk (opaque hash filenames, bounded to 32 MiB); profile image selection is cached in memory. Images are served to both desktop windows through a local WebView2 virtual host. VRChat auth cookies are used only for the fixed VRChat API host, never forwarded to CDN hosts or JavaScript. Redirects, response sizes and raster signatures are checked; unavailable images fall back without blocking the roster. Cached portraits persist across app restarts; they may be removed by deleting the PlayerImages cache while ELIXR is closed.

The native tag service downloads the three public FewTags/ETags/etags-groups catalogs from raw.githubusercontent.com through a separate cookie-free HTTP client. GitHub never receives VRChat credentials, encountered player IDs, or per-player tag queries. Public feed copies are cached locally under `%LOCALAPPDATA%/ELIXR/PlayerList/TagFeeds`. While signed in, the service requests encountered players' public group memberships from VRChat's HTTPS API to match group IDs against the group catalog. Membership results are held in a bounded memory cache, not persisted, and are cleared on account change. Rate limits and network failures never prevent local roster display.

Registration is not connected. A future ELIXR endpoint must independently verify identity and store only the unique authenticated VRChat user ID. Current authentication sends secrets only to VRChat's HTTPS API. No instances, encountered players, launch counts, hardware identifiers, fingerprints or activity history are sent to ELIXR.

Remember me stores only the auth/twoFactorAuth session cookies and their expiry in session.bin encrypted with Windows DPAPI CurrentUser. Passwords and verification codes are never written to storage or logs. Without Remember me, cookies live only in memory. Startup validates the saved session; rejected/expired sessions are removed, while a temporary network failure allows retry. Logout clears memory and the saved session and attempts server logout. Recent-player persistence and registration remain unimplemented.
