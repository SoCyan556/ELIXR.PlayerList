using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;
namespace ELIXR.PlayerList.Infrastructure.Services;

// All credentials and session tokens remain in this native service. Only the public account DTO crosses the UI bridge.
public sealed class VrchatAuthenticationService(HttpClient http, CookieContainer cookies, ISessionStore store) : IAuthenticationService
{
    private static readonly Uri Api = new("https://api.vrchat.cloud/api/1/");
    private bool _remember;
    private string[] _methods = [];
    private DateTimeOffset _retryAfter;
    public AuthenticatedUser? CurrentUser { get; private set; }
    public string? SessionWarning { get; private set; }
    private sealed record SessionCookie(string Name, string Value, DateTime? Expires);

    public async Task<AuthenticationResult> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        if (DateTimeOffset.UtcNow < _retryAfter) return new(AuthenticationStatus.Failed, Message: "VRChat asked us to wait. Please try again later.");
        CurrentUser = null;
        try
        {
            var saved = await store.ReadAsync(cancellationToken);
            if (saved is null) return new(AuthenticationStatus.SignedOut);
            var entries = JsonSerializer.Deserialize<SessionCookie[]>(saved) ?? [];
            foreach (var entry in entries.Where(e => e.Name is "auth" or "twoFactorAuth"))
            {
                if (entry.Expires is { } expiry && expiry <= DateTime.UtcNow) continue;
                var cookie = new Cookie(entry.Name, entry.Value, "/", Api.Host) { Secure = true, HttpOnly = true };
                if (entry.Expires is { } expires) cookie.Expires = expires;
                cookies.Add(cookie);
            }
            if (cookies.GetCookies(Api)["auth"] is null) { await store.DeleteAsync(cancellationToken); return new(AuthenticationStatus.SignedOut); }
            _remember = true;
            using var response = await SendAsync(HttpMethod.Get, "auth/user", null, null, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ClearCookies();
                await store.DeleteAsync(cancellationToken);
                return new(AuthenticationStatus.SignedOut, Message: "Your saved session expired. Sign in again.");
            }
            return await ReadUserAsync(response, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or JsonException or CookieException)
        {
            return new(AuthenticationStatus.Failed, Message: "Could not restore your session. Check your connection and try again.");
        }
    }

    public async Task<AuthenticationResult> LoginAsync(string username, string password, bool remember, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return new(AuthenticationStatus.Failed, Message: "Enter your username and password.");
        if (DateTimeOffset.UtcNow < _retryAfter) return new(AuthenticationStatus.Failed, Message: "VRChat asked us to wait. Please try again later.");
        CurrentUser = null;
        ClearCookies();
        _methods = [];
        _remember = remember;
        await store.DeleteAsync(token); // An explicit new login replaces any previously remembered account.
        try
        {
            var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.EscapeDataString(username.Trim()) + ":" + Uri.EscapeDataString(password)));
            using var response = await SendAsync(HttpMethod.Get, "auth/user", null, value, token);
            return await ReadUserAsync(response, token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { return new(AuthenticationStatus.Failed, Message: "Could not reach VRChat. Check your connection and try again."); }
    }

    public async Task<AuthenticationResult> VerifyAsync(string method, string code, CancellationToken token = default)
    {
        if (!_methods.Contains(method)) return new(AuthenticationStatus.Failed, Message: "Choose a verification method offered by VRChat.");
        if (DateTimeOffset.UtcNow < _retryAfter) return new(AuthenticationStatus.VerificationRequired, VerificationMethods: _methods, Message: "Please wait before trying another code.");
        code = code.Replace(" ", "").Replace("-", "").Trim();
        if (code.Length is < 4 or > 12 || !code.All(char.IsAsciiLetterOrDigit))
            return new(AuthenticationStatus.VerificationRequired, VerificationMethods: _methods, Message: "Enter a valid verification code.");
        try
        {
            var route = method == "emailOtp" ? "auth/twofactorauth/emailotp/verify" : method == "otp" ? "auth/twofactorauth/otp/verify" : "auth/twofactorauth/totp/verify";
            using var response = await SendAsync(HttpMethod.Post, route, new { code }, null, token);
            if (!response.IsSuccessStatusCode) return new(AuthenticationStatus.VerificationRequired, VerificationMethods: _methods, Message: ErrorMessage(response.StatusCode, true));
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (!json.RootElement.TryGetProperty("verified", out var verified) || verified.ValueKind != JsonValueKind.True)
                return new(AuthenticationStatus.VerificationRequired, VerificationMethods: _methods, Message: "The verification code was not accepted.");
            using var user = await SendAsync(HttpMethod.Get, "auth/user", null, null, token);
            return await ReadUserAsync(user, token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { return new(AuthenticationStatus.VerificationRequired, VerificationMethods: _methods, Message: "Verification could not reach VRChat. Try again."); }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, object? body, string? basic, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, new Uri(Api, route));
        request.Headers.TryAddWithoutValidation("User-Agent", "ELIXRPlayerList/0.1.0 (+support: https://elixr.gg/support)");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (basic is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request, token);
        if (response.Headers.TryGetValues("Set-Cookie", out var headers))
            foreach (var header in headers) cookies.SetCookies(Api, header);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);
            _retryAfter = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(wait);
        }
        return response;
    }

    private async Task<AuthenticationResult> ReadUserAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode) return new(AuthenticationStatus.Failed, Message: ErrorMessage(response.StatusCode));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;
        if (root.TryGetProperty("requiresTwoFactorAuth", out var methods) && methods.ValueKind == JsonValueKind.Array)
        {
            _methods = methods.EnumerateArray().Select(e => e.GetString()).Where(m => m is "totp" or "emailOtp" or "otp").Cast<string>().Distinct().ToArray();
            if (_methods.Length == 0) return new(AuthenticationStatus.Failed, Message: "VRChat requested a verification method this app does not support.");
            return new(AuthenticationStatus.VerificationRequired, VerificationMethods: _methods);
        }
        var id = root.TryGetProperty("id", out var userId) ? userId.GetString() : null;
        var name = root.TryGetProperty("displayName", out var displayName) ? displayName.GetString() : null;
        if (id is null || !id.StartsWith("usr_") || !Guid.TryParseExact(id[4..], "D", out _) || string.IsNullOrEmpty(name))
            return new(AuthenticationStatus.Failed, Message: "VRChat did not return a valid account.");
        CurrentUser = new(id, name);
        _methods = [];
        SessionWarning = null;
        if (_remember)
        {
            var entries = cookies.GetCookies(Api).Cast<Cookie>().Where(c => c.Name is "auth" or "twoFactorAuth")
                .Select(c => new SessionCookie(c.Name, c.Value, c.Expires == DateTime.MinValue ? null : c.Expires.ToUniversalTime())).ToArray();
            if (entries.All(c => c.Name != "auth")) SessionWarning = "Signed in, but VRChat did not return a session to remember.";
            else
            {
                try { await store.WriteAsync(JsonSerializer.Serialize(entries), token); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
                { SessionWarning = "Signed in for this session. Windows could not save your session securely."; }
            }
        }
        return new(AuthenticationStatus.SignedIn, CurrentUser, Message: SessionWarning);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (cookies.GetCookies(Api)["auth"] is not null)
            {
                using var response = await SendAsync(HttpMethod.Put, "logout", null, null, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
        finally
        {
            CurrentUser = null;
            _methods = [];
            ClearCookies();
            await store.DeleteAsync(CancellationToken.None);
        }
    }
    private void ClearCookies() { foreach (Cookie cookie in cookies.GetAllCookies()) cookie.Expired = true; }
    private static string ErrorMessage(HttpStatusCode code, bool verification = false) => code switch
    {
        HttpStatusCode.TooManyRequests => "VRChat asked us to wait. Please try again later.",
        HttpStatusCode.Unauthorized => verification ? "Invalid or expired code. Try again." : "The username or password was not accepted.",
        HttpStatusCode.Forbidden => "VRChat blocked this sign-in. Check your account or verification requirements.",
        _ => "VRChat is temporarily unavailable. Please try again."
    };
}

