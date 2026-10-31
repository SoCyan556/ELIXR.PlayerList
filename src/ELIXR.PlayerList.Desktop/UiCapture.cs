#if DEBUG
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace ELIXR.PlayerList.Desktop;

public partial class MainWindow
{
    private async Task CaptureUiStatesAsync()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "--capture-ui");
        if (!IsUiPreview || index < 0 || index + 1 >= args.Length) return;
        var folder = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(folder);
        var checks = new List<object>();
        var imageName = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant() + ".png";
        var imageUrl = "https://avatars.elixr.example/" + imageName;
        string? testImagePath = null;
        if (_imageCacheDirectory is not null)
        {
            Directory.CreateDirectory(_imageCacheDirectory);
            testImagePath = Path.Combine(_imageCacheDirectory, imageName);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 255, 128, 64, 255 }, 4)));
            using var output = File.Create(testImagePath); png.Save(output);
        }
        async Task Script(string script) { await Browser.CoreWebView2.ExecuteScriptAsync("(()=>{" + script + "})()"); await Task.Delay(650); }
        async Task Check(string name, string expression)
        {
            var passed = await Browser.CoreWebView2.ExecuteScriptAsync(expression);
            checks.Add(new { name, passed = passed == "true" });
        }
        async Task Capture(string name)
        {
            await Task.Delay(120);
            await using var webImage = new MemoryStream();
            await Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, webImage);
            webImage.Position = 0;
            var source = new BitmapImage();
            source.BeginInit(); source.CacheOption = BitmapCacheOption.OnLoad; source.StreamSource = webImage; source.EndInit();
            var native = NativeFrameCapture.Capture(this, out var bounds);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(native, new Rect(0, 0, native.PixelWidth, native.PixelHeight));
                var offset = Browser.PointToScreen(new Point(0, 0));
                dc.DrawImage(source, new Rect(offset.X-bounds.Left, offset.Y-bounds.Top, source.PixelWidth, source.PixelHeight));
            }
            var output = new RenderTargetBitmap(native.PixelWidth, native.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            output.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            await using var file = File.Create(Path.Combine(folder, name + ".png"));
            encoder.Save(file);
        }
        const string clickCompact = "document.querySelector('.tag-controls button[aria-label=\"Compact Tags\"]').click()";
        if (args.Contains("--ordering-only"))
        {
            await Script("window.__elixrPreview.reset();window.__elixrPreview.ordering()");
            await Check("self friends and others sort correctly", "[...document.querySelectorAll('.player-name strong')].map(e=>e.textContent).join('|')==='You · Demo|Amy · Demo|Zoe · Demo|Aaron · Demo|Bella · Demo'");
            await Check("friend groups have separate headings", "[...document.querySelectorAll('.player-section>span:first-child')].map(e=>e.textContent).join('|')==='You|Friends|Other players'");
            await Capture("ordered-player-list");
            await Script("const input=document.querySelector('.search input');input.value='Zoe';input.dispatchEvent(new Event('input',{bubbles:true}))");
            await Check("search hides empty sections", "document.querySelectorAll('.player-row').length===1&&document.querySelectorAll('.player-section').length===1&&document.querySelector('.player-section').textContent.includes('Friends')");
            await Script("[...document.querySelectorAll('nav button')].find(b=>b.textContent.trim()==='Recent').click()");
            await Check("Recent retains encounter order without friend sections", "!document.querySelector('.player-section')&&[...document.querySelectorAll('.player-name strong')].map(e=>e.textContent).join('|')==='Bella · Demo|Zoe · Demo|Amy · Demo'");
            await File.WriteAllTextAsync(Path.Combine(folder,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
            if(testImagePath is not null)File.Delete(testImagePath);
            Close();return;
        }
        if (args.Contains("--recent-only"))
        {
            await Script("window.__elixrPreview.reset();window.__elixrPreview.recent();[...document.querySelectorAll('nav button')].find(b=>b.textContent.trim()==='Recent').click()");
            await Check("Recent uses history rather than current roster", "document.querySelectorAll('.player-row').length===3");
            await Check("Recent shows encounter times", "document.querySelectorAll('.recent-seen').length===3");
            await Check("History explains local retention", "document.querySelector('.recent-history-help').textContent.includes('30 days')");
            await Capture("recent-list");
            await Script("document.querySelector('.player-row').click()");
            await Check("Recent opens the existing profile", "!!document.querySelector('.profile-panel')");
            await Check("Recent does not show current instance as historic context", "!document.querySelector('.profile-details').textContent.includes('Instance')");
            await Capture("recent-profile");
            await Script("document.querySelector('.close-profile').click();const input=document.querySelector('.search input');input.value='no match for this history';input.dispatchEvent(new Event('input',{bubbles:true}))");
            await Check("Recent search filters saved records", "document.querySelectorAll('.player-row').length===0&&document.querySelector('.empty-state').textContent.includes('No matching players')");
            await File.WriteAllTextAsync(Path.Combine(folder, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
            if (testImagePath is not null) File.Delete(testImagePath);
            Close(); return;
        }
        if (args.Contains("--search-only"))
        {
            await Script("window.__elixrPreview.reset();[...document.querySelectorAll('nav button')].find(b=>b.textContent.trim()==='Search').click()");
            await Check("Search is a fifth navigation section", "document.querySelectorAll('nav button').length===5&&!!document.querySelector('.user-search-form')");
            await Check("Search is disabled in fictional preview", "document.querySelector('.user-search-form button').disabled");
            await Capture("search-initial");
            const string fixture = "{vrchatUserId:'usr_11111111-2222-3333-4444-555555555555',displayName:'Search fixture · fictional',tags:[]}";
            await Script("const input=document.querySelector('#vrchat-user-query');input.value='fixture';input.dispatchEvent(new Event('input',{bubbles:true}));document.querySelector('.user-search-form').dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}))");
            await Check("Search shows a loading state", "document.querySelector('[aria-label=\"VRChat search results\"]').getAttribute('aria-busy')==='true'");
            await Script("window.dispatchEvent(new CustomEvent('elixr:user-search',{detail:{type:'users.results',requestId:window.__elixrSearchRequest,users:[" + fixture + "],hasMore:true}}))");
            await Check("Search results are separate from roster", "document.querySelectorAll('.user-search-page .player-row').length===1&&document.querySelector('.player-count')===null");
            await Script("document.querySelector('.user-search-page .player-row').click()");
            await Check("Selecting a result checks tags", "document.querySelector('.search-checking').textContent.includes('Checking tags')");
            await Script("window.dispatchEvent(new CustomEvent('elixr:user-search',{detail:{type:'users.detail',requestId:window.__elixrSearchRequest,user:{..." + fixture + ",tagsChecked:true,tags:[{name:'Fixture tag',category:'Custom',color:'#ff80dd'}]},feeds:[]}}))");
            await Check("Lookup renders shared profile and tags", "document.querySelector('.profile-name h2').textContent.includes('Search fixture')&&document.querySelector('.profile-tags').textContent.includes('Fixture tag')");
            await Check("Search does not pretend to be an encounter", "!document.querySelector('.profile-details').textContent.includes('Last encountered')");
            await Capture("search-tags");
            await Script("document.querySelector('.close-profile').click();window.dispatchEvent(new CustomEvent('elixr:user-search',{detail:{type:'users.detail',requestId:window.__elixrSearchRequest-1,user:" + fixture + "}}))");
            await Check("Closed profiles ignore stale replies", "!document.querySelector('.profile-panel')");
            await Script("document.querySelector('.user-search-page .player-row').click();window.dispatchEvent(new CustomEvent('elixr:user-search',{detail:{type:'users.error',requestId:window.__elixrSearchRequest,message:'Diagnostic unavailable'}}))");
            await Check("Failed checks offer retry instead of no tags", "document.querySelector('.search-checking').textContent.includes('Retry')&&document.querySelector('[role=alert]').textContent.includes('Diagnostic unavailable')");
            await File.WriteAllTextAsync(Path.Combine(folder, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
            if (testImagePath is not null) File.Delete(testImagePath);
            Close(); return;
        }
        await Script("window.__elixrPreview.reset()");
        await Check("18 fictional players", "document.querySelectorAll('.player-row').length===18");
        await Check("sidebar 150px", "document.querySelector('.sidebar').getBoundingClientRect().width===150");
        await Check("footer is version only", "document.querySelector('.sidebar-footer').textContent.trim()==='v0.1.0'");
        await File.WriteAllTextAsync(Path.Combine(folder, "layout.json"), await Browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({row:document.querySelector('.player-row').getBoundingClientRect().height,sidebar:document.querySelector('.sidebar').getBoundingClientRect().width,viewport:innerWidth,scale:devicePixelRatio})"));
        await Check("54px row", "document.querySelector('.player-row').offsetHeight===54");
        await Capture("01-full-tags");
        await Browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.player-row').click()");
        await Task.Delay(45);
        var movingPanel = await Browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.profile-slot')?.getBoundingClientRect().x");
        await Task.Delay(700);
        var settledPanel = await Browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.profile-slot')?.getBoundingClientRect().x");
        checks.Add(new { name = "profile spring moves and settles", passed = movingPanel != settledPanel && movingPanel != "null" && settledPanel != "null" });
        await Script("document.querySelector('.close-profile').click()");
        await Script("document.querySelector('.player-row').click()");
        await Check("profile opens at 285px", "document.querySelector('.profile-panel').getBoundingClientRect().width===285");
        await Capture("02-full-profile");
        await Check("group name and full code rendered in profile", "document.querySelector('.profile-group-name')?.textContent==='ELIXR Preview Group'&&document.querySelector('.profile-group-code code')?.textContent==='ELIXR.0042'");
        await Script("const clipboard=navigator.clipboard,original=clipboard.writeText;try{window.__groupCopy=null;clipboard.writeText=async value=>{window.__groupCopy=value};if(clipboard.writeText!==original)document.querySelector('.profile-group-code button').click()}finally{clipboard.writeText=original}");
        await Check("copy group code uses exact code and confirms", "window.__groupCopy==='ELIXR.0042'&&document.querySelector('.action-notice').textContent==='Group code copied'");
        await Script("document.querySelectorAll('.player-row')[3].click()");
        await Check("unavailable group code is not copyable", "document.querySelector('.profile-group-code code')?.textContent==='Code unavailable'&&document.querySelector('.profile-group-code button').disabled");
        await Script("document.querySelector('.player-row').click()");
        await Script("window.__elixrPreview.avatar(" + JsonSerializer.Serialize(imageUrl) + ")");
        await Check("cached image loads in player list", "document.querySelector('.player-row .avatar img')?.naturalWidth===1");
        await Check("cached image loads in profile", "document.querySelector('.profile-header .avatar img')?.naturalWidth===1");
        await Script("window.__elixrPreview.avatar('data:image/png;base64,not-an-image')");
        await File.WriteAllTextAsync(Path.Combine(folder, "avatar-fallback-state.json"), await Browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({html:document.querySelector('.player-row .avatar')?.innerHTML,complete:document.querySelector('.player-row .avatar img')?.complete,width:document.querySelector('.player-row .avatar img')?.naturalWidth})"));
        await Check("missing profile image shows safe icon", "!!document.querySelector('.player-row').querySelector('.avatar svg')&&!document.querySelector('.player-row').querySelector('.avatar img')");
        await Script("window.__elixrPreview.avatar(" + JsonSerializer.Serialize(imageUrl) + ")");
        await Check("profile image recovers after fallback", "document.querySelector('.player-row .avatar img')?.naturalWidth===1");
        await Script("window.__elixrPreview.resetAvatar()");
        if (args.Contains("--images-only"))
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
            if (testImagePath is not null) File.Delete(testImagePath);
            Close(); return;
        }
        await Check("feed color in list", "getComputedStyle(document.querySelector('.player-row .tag-text>span')).color==='rgb(255, 77, 109)'");
        await Check("feed color in profile", "getComputedStyle(document.querySelector('.profile-tag .tag-text>span')).color==='rgb(255, 77, 109)'");
        await Check("source animation active", "getComputedStyle(document.querySelector('.tag-effect-pulse')).animationName==='tag-pulse'");
        await Check("rich color runs preserved", "[...document.querySelectorAll('.player-row')[1].querySelectorAll('.tag-text')].some(t=>t.textContent==='Frequent'&&t.children.length===2)");
        await Script("document.querySelectorAll('.player-row')[1].click()");
        await Check("select another player", "document.querySelector('.profile-name h2').textContent==='Kuro · Demo'");
        await Script("document.querySelector('.close-profile').click()");
        await Check("profile closes", "!document.querySelector('.profile-panel')");
        await Script(clickCompact);
        await Check("one icon per category", "document.querySelector('.player-row').querySelectorAll('.tag.compact').length===2");
        await Capture("03-compact-tags");
        await Script("document.querySelector('.player-row').click()");
        await Capture("04-compact-profile");
        foreach (var category in new[] { "Malicious", "Fun / Custom", "Group" })
        {
            await Script("document.querySelector('.tag-controls button[aria-label=" + JsonSerializer.Serialize(category) + "]').click()");
        }
        await Check("all categories hide in compact list and profile", "document.querySelectorAll('.tag,.profile-tag').length===0");
        await Script(clickCompact);
        await Check("categories also hide in full mode", "document.querySelectorAll('.tag,.profile-tag').length===0");
        await Script("window.__elixrPreview.reset(); document.querySelector('.close-profile').click()");
        await Script("const input=document.querySelector('.search input');input.value='Fictional player';input.dispatchEvent(new Event('input',{bubbles:true}))");
        await Check("immediate name search", "document.querySelectorAll('.player-row').length===1");
        await Script("document.querySelector('.player-row').click()");
        await Check("many tags preserved", "document.querySelector('.player-row').querySelectorAll('.tag').length===5");
        await Capture("07-long-name-many-tags");
        await Script("document.querySelector('.close-profile').click();const input=document.querySelector('.search input');input.value='preview_fictional_02';input.dispatchEvent(new Event('input',{bubbles:true}))");
        await Check("user ID search", "document.querySelectorAll('.player-row').length===1 && document.querySelector('.player-name strong').textContent==='Kuro · Demo'");
        await Script("const input=document.querySelector('.search input');input.value='';input.dispatchEvent(new Event('input',{bubbles:true}));window.__elixrPreview.empty()");
        await Capture("05-empty");
        await Script("window.__elixrPreview.reset()");
        await Script("document.querySelector('.player-row').click()");
        Width = 760;
        await Task.Delay(200);
        await Check("narrow drawer leaves list grid uncompressed", "getComputedStyle(document.querySelector('.profile-slot')).position==='absolute'");
        await Check("no horizontal document overflow", "document.documentElement.scrollWidth<=innerWidth");
        await Capture("06-narrow-profile");
        await Script("document.querySelector('.filter-menu').click()");
        await Check("narrow filter menu opens", "document.querySelector('.filter-controls').classList.contains('open')");
        await Capture("08-narrow-filters");
        Width = 1180;
        await Script("document.querySelector('.close-profile').click();document.querySelector('.filter-menu').click()");
        foreach (var name in new[] { "Home", "Recent", "Settings", "Players" })
        {
            await Script("[...document.querySelectorAll('nav button')].find(b=>b.textContent.trim()===" + JsonSerializer.Serialize(name) + ").click()");
            await Check("navigation " + name, "document.querySelector('h1').textContent===" + JsonSerializer.Serialize(name == "Players" ? "Player List" : name == "Recent" ? "Recent Players" : name));
            if (name == "Settings")
            {
                await Check("AI assistance statement", "document.querySelector('.settings-page').textContent.includes('This app was created with the assistance of AI. We fully understand the concerns people have about its use.')");
                await Capture("12-settings-statements");
            }
        }
        await Script("window.__elixrPreview.auth('SignedOut')");
        await Check("login and remember me rendered", "!!document.querySelector('.remember input')&&!!document.querySelector('input[type=password]')");
        await Capture("09-sign-in");
        await Script("document.querySelector('[aria-label=\"Show password\"]').click()");
        await Check("password visibility action", "!!document.querySelector('input[autocomplete=\"current-password\"][type=text]')");
        await Script("window.__elixrPreview.auth('VerificationRequired')");
        await Check("verification methods offered", "document.querySelectorAll('.login-form select option').length===2");
        await Capture("10-verification");
        await Script("window.__elixrPreview.auth('Restoring')");
        await Capture("11-restore-session");
        await Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", """{"features":[{"name":"prefers-reduced-motion","value":"reduce"}]}""");
        await Task.Delay(120);
        await Check("reduced motion disables loader animation", "matchMedia('(prefers-reduced-motion: reduce)').matches && getComputedStyle(document.querySelector('.session-loader span')).animationName==='none'");
        await Script("window.__elixrPreview.reset()");
        await Check("reduced motion disables source tag effects", "[...document.querySelectorAll('.tag-text,.tag-text>span')].every(t=>getComputedStyle(t).animationName==='none')");
        await Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", """{"features":[]}""");
        await Script("window.__elixrPreview.reset()");
        await Script("document.querySelector('.desktop-overlay-button').click()");
        for (var attempt = 0; attempt < 100 && _desktopOverlay?.IsReady != true; attempt++) await Task.Delay(100);
        checks.Add(new { name = "desktop overlay is ready and topmost", passed = _desktopOverlay is { IsReady: true, Topmost: true, ShowInTaskbar: false, Owner: null } });
        if (_desktopOverlay is { IsReady: true } floating)
        {
            await Task.Delay(600);
            checks.Add(new { name = "overlay renders shared player list without main navigation", passed = await floating.InspectAsync("document.querySelectorAll('.player-row').length===18&&!document.querySelector('.sidebar')&&!!document.querySelector('.desktop-overlay')") == "true" });
            checks.Add(new { name = "native WebView drag regions enabled", passed = floating.NativeDragEnabled && await floating.InspectAsync("getComputedStyle(document.querySelector('.overlay-header')).getPropertyValue('app-region')==='drag'&&getComputedStyle(document.querySelector('.overlay-header button')).getPropertyValue('app-region')==='no-drag'") == "true" });
            checks.Add(new { name = "overlay HWND clips all four backing corners", passed = floating.HasClippedCorners });
            checks.Add(new { name = "overlay uses the real ELIXR logo", passed = await floating.InspectAsync("document.querySelector('.overlay-logo')?.naturalWidth>0&&!document.querySelector('.overlay-emblem')") == "true" });
            var nativeOverlayPng = new PngBitmapEncoder();
            nativeOverlayPng.Frames.Add(BitmapFrame.Create(NativeFrameCapture.Capture(floating, out _)));
            await using (var nativeOverlayFile = File.Create(Path.Combine(folder, "17-overlay-native-frame.png"))) nativeOverlayPng.Save(nativeOverlayFile);
            var beforeEdit = floating.Placement;
            await floating.InspectAsync("document.querySelector('[aria-label=\"Resize overlay\"]').click()");
            await Task.Delay(300);
            checks.Add(new { name = "resize button opens dimmed desktop editor", passed = floating.ResizeEditor is { IsVisible:true, AllowsTransparency:true, Topmost:true } });
            if (floating.ResizeEditor is { } resizeEditor)
            {
                resizeEditor.PreviewSize(520,620);
                await Task.Delay(150);
                checks.Add(new { name = "resize controls update live overlay", passed = floating.Width == 520 && floating.Height == 620 });
                checks.Add(new { name = "resized overlay HWND retains corner clipping", passed = floating.HasClippedCorners });
                var resizeBitmap = new RenderTargetBitmap((int)resizeEditor.ActualWidth,(int)resizeEditor.ActualHeight,96,96,PixelFormats.Pbgra32);
                resizeBitmap.Render(resizeEditor);
                var resizePng = new PngBitmapEncoder();resizePng.Frames.Add(BitmapFrame.Create(resizeBitmap));
                await using(var resizeFile=File.Create(Path.Combine(folder,"16-overlay-resize-editor.png"))) resizePng.Save(resizeFile);
                resizeEditor.CancelAndClose();await Task.Delay(250);
                checks.Add(new { name = "cancel restores original overlay bounds", passed = floating.Placement == beforeEdit && floating.ResizeEditor is null });
            }
            floating.OpenResizeEditor();await Task.Delay(250);
            if(floating.ResizeEditor is { } saveEditor)
            {
                saveEditor.PreviewSize(420,560);
                var didSave=await saveEditor.SaveAsync();await Task.Delay(250);
                checks.Add(new { name = "resize save persists exact dimensions", passed = didSave&&_settings.Current.DesktopOverlayPlacement is {Width:420,Height:560}&&floating.ResizeEditor is null });
                var reloaded = new ELIXR.PlayerList.Infrastructure.Services.JsonSettingsService(Path.Combine(Path.GetTempPath(),"elixr-ui-preview-settings.json"));await reloaded.LoadAsync();
                checks.Add(new { name = "saved dimensions survive settings reload", passed = reloaded.Current.DesktopOverlayPlacement is {Width:420,Height:560} });
            }
            await floating.InspectAsync("window.__elixrPreview.avatar(" + JsonSerializer.Serialize(imageUrl) + ")");
            await Task.Delay(400);
            checks.Add(new { name = "cached profile image loads in desktop overlay", passed = await floating.InspectAsync("document.querySelector('.player-row .avatar img')?.naturalWidth===1") == "true" });
            await floating.InspectAsync("window.__elixrPreview.resetAvatar()");
            await Check("desktop overlay header toggle is active", "document.querySelector('.desktop-overlay-button').getAttribute('aria-pressed')==='true'");
            WindowState = WindowState.Minimized;
            await Task.Delay(150);
            checks.Add(new { name = "overlay survives main window minimize", passed = floating.IsVisible && floating.WindowState != WindowState.Minimized });
            WindowState = WindowState.Normal;
            floating.Width = 400; floating.Height = 520;
            await Task.Delay(200);
            await floating.CaptureAsync(Path.Combine(folder, "13-desktop-overlay.png"));
            checks.Add(new { name = "compact desktop overlay has no horizontal overflow", passed = await floating.InspectAsync("document.documentElement.scrollWidth<=innerWidth") == "true" });
            await floating.InspectAsync("document.querySelector('button[aria-label=\"Search players\"]').click()");
            await Task.Delay(150);
            checks.Add(new { name = "overlay search button reveals and focuses search", passed = await floating.InspectAsync("document.activeElement===document.querySelector('.search input')&&getComputedStyle(document.querySelector('.search')).display!=='none'") == "true" });
            await floating.InspectAsync("document.querySelector('input').value='Kuro';document.querySelector('input').dispatchEvent(new Event('input',{bubbles:true}))");
            await Task.Delay(200);
            checks.Add(new { name = "desktop overlay search works", passed = await floating.InspectAsync("document.querySelectorAll('.player-row').length===1") == "true" });
            await floating.InspectAsync("document.querySelector('.player-row').click()");
            await Task.Delay(400);
            checks.Add(new { name = "overlay row expands with disabled actions for pending identity", passed = await floating.InspectAsync("document.querySelector('.player-row').getAttribute('aria-expanded')==='true'&&document.querySelectorAll('.overlay-player-actions button:disabled').length===2") == "true" });
            await floating.InspectAsync("document.querySelector('input').value='';document.querySelector('input').dispatchEvent(new Event('input',{bubbles:true}))");
            await Task.Delay(250);
            await floating.InspectAsync("document.querySelector('.player-row').click()");
            await Task.Delay(450);
            checks.Add(new { name = "overlay permits only one expanded row", passed = await floating.InspectAsync("document.querySelectorAll('.player-row[aria-expanded=true]').length===1&&document.querySelectorAll('.overlay-player-actions button:not(:disabled)').length===2") == "true" });
            await floating.CaptureAsync(Path.Combine(folder, "14-overlay-player-actions.png"));
            // Intercept just this click to verify the command without modifying the user's clipboard.
            checks.Add(new { name = "copy button sends exact selected ID", passed = await floating.InspectAsync("(()=>{const bridge=window.chrome.webview,original=bridge.postMessage;try{bridge.postMessage=command=>window.__copyCommand=command;if(bridge.postMessage===original)return false;document.querySelector('.overlay-player-actions button').click();return window.__copyCommand?.type==='profile.copyId'&&window.__copyCommand.userId==='usr_00000000-0000-0000-0000-000000000001'}finally{bridge.postMessage=original}})()") == "true" });
            await Script("[...document.querySelectorAll('nav button')].find(b=>b.textContent.trim()==='Settings').click()");
            WindowState = WindowState.Minimized;
            await floating.InspectAsync("document.querySelectorAll('.overlay-player-actions button')[1].click()");
            await Task.Delay(650);
            checks.Add(new { name = "open in app restores main window", passed = WindowState == WindowState.Normal });
            await Check("open in app selects the correct in-app profile", "document.querySelector('h1').textContent==='Player List'&&document.querySelector('.profile-name h2')?.textContent==='Mizuki · Demo'");
            await floating.InspectAsync("window.chrome.webview.postMessage({type:'profile.openInApp',userId:'usr_invalid'})");
            await Task.Delay(100);
            await Check("native bridge rejects malformed profile IDs", "document.querySelector('.profile-name h2')?.textContent==='Mizuki · Demo'&&!!document.querySelector('[role=alert]')");
            await floating.InspectAsync("document.querySelector('.desktop-overlay').dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))");
            await Task.Delay(900);
            await File.WriteAllTextAsync(Path.Combine(folder, "escape-state.json"), await floating.InspectAsync("JSON.stringify({expanded:document.querySelectorAll('.player-row[aria-expanded=true]').length,actions:document.querySelectorAll('.overlay-player-actions').length,height:document.querySelector('.player-row-actions')?.getBoundingClientRect().height})"));
            checks.Add(new { name = "Escape collapses overlay dropdown", passed = await floating.InspectAsync("!document.querySelector('.overlay-player-actions')") == "true" });
            await floating.InspectAsync("window.__elixrPreview.reset();document.querySelector('button[aria-label=\"Search players\"]').click();document.querySelector('button[aria-label=\"Overlay settings\"]').click()");
            await Task.Delay(350);
            checks.Add(new { name = "overlay settings menu is functional", passed = await floating.InspectAsync("!!document.querySelector('.overlay-settings')&&document.querySelectorAll('.overlay-settings [role=switch]').length===5") == "true" });
            await floating.InspectAsync("document.querySelector('[aria-label=\"Compact layout\"]').click()");
            await Task.Delay(500);
            checks.Add(new { name = "compact layout resizes native overlay and uses category icons", passed = floating.Width == 270 && await floating.InspectAsync("document.querySelector('.desktop-overlay').classList.contains('overlay-compact')&&document.querySelectorAll('.player-row .tag:not(.compact)').length===0") == "true" });
            await floating.InspectAsync("document.querySelector('[aria-label=\"Close overlay settings\"]').click()");
            await Task.Delay(400);
            await floating.CaptureAsync(Path.Combine(folder, "15-compact-overlay.png"));
            checks.Add(new { name = "compact reference layout has no horizontal overflow", passed = await floating.InspectAsync("document.documentElement.scrollWidth<=innerWidth") == "true" });
            await floating.InspectAsync("document.querySelector('button[aria-label=\"Overlay settings\"]').click()");
            await Task.Delay(300);
            await floating.InspectAsync("document.querySelector('[aria-label=\"Compact layout\"]').click()");
            await Task.Delay(300);
            checks.Add(new { name = "expanded layout can be restored", passed = floating.Width == 440 });
            floating.Width = 400;
            await floating.InspectAsync("document.querySelector('[aria-label=\"Hide desktop overlay\"]').click()");
            await Task.Delay(200);
            checks.Add(new { name = "overlay close synchronizes main toggle", passed = _desktopOverlay is null });
            await Check("desktop overlay header toggle is inactive", "document.querySelector('.desktop-overlay-button').getAttribute('aria-pressed')==='false'");
            SetDesktopOverlayVisible(true);
            await Task.Delay(200);
            checks.Add(new { name = "overlay remembers session size", passed = _desktopOverlay?.Width == 400 && _desktopOverlay.Height == 520 });
            SetDesktopOverlayVisible(false);
        }
        await Script("window.__elixrPreview.update()");
        await Check("update dialog offers Update now and Maybe later", "!!document.querySelector('[role=dialog][aria-modal=true]')&&[...document.querySelectorAll('.update-actions button')].map(b=>b.textContent.trim()).join('|')==='Maybe later|Update now'");
        await Check("update prompt receives keyboard focus", "document.activeElement===document.querySelector('.update-actions button')");
        await Capture("18-update-prompt");
        await Script("document.querySelector('.update-actions button').click()");
        await Check("Maybe later dismisses without installing", "!document.querySelector('.update-dialog')");
        await File.WriteAllTextAsync(Path.Combine(folder, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        if (testImagePath is not null) File.Delete(testImagePath); // This diagnostic's own generated 1px fixture only.
        Close();
    }
}
#endif

