using System.IO;
using System.Net.Http;
using System.Text.Json;
using ELIXR.PlayerList.Infrastructure.Services;

namespace ELIXR.PlayerList.Desktop;

public partial class MainWindow
{
    internal VrchatUserSearchService? UserSearch { get; init; }
    internal VrchatProfileImageService? SearchImages { get; init; }
    private CancellationTokenSource? _searchCancellation;
    private readonly Dictionary<string, UserSearchResult> _searchResults = [];
    private void ClearSearch()
    {
        _searchCancellation?.Cancel();
        _searchResults.Clear();
    }
    private async Task HandleSearchAsync(JsonElement message, bool lookup)
    {
        var requestId = message.GetProperty("requestId").GetInt32();
        var account = _authentication.CurrentUser?.VrchatUserId;
        if (account is null || UserSearch is null || _tagFeeds is null || IsUiPreview) return;
        _searchCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        _searchCancellation = cancellation;
        try
        {
            if (lookup)
            {
                var id = ValidatedUserId(message);
                if (!_searchResults.TryGetValue(id, out var user)) throw new InvalidOperationException("Search for this user again before checking tags.");
                var tags = await _tagFeeds.GetTagsAsync(id, cancellation.Token);
                var avatar = SearchImages is null ? null : await SearchImages.GetAsync(id, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (_authentication.CurrentUser?.VrchatUserId != account) return;
                user = user with { Tags = tags, TagsChecked = true, AvatarUrl = avatar };
                _searchResults[id] = user;
                SearchPost(new { type = "users.detail", requestId, user, feeds = _tagFeeds.Status });
            }
            else
            {
                _searchResults.Clear();
                var query = message.GetProperty("query").GetString() ?? "";
                var page = message.GetProperty("page").GetInt32();
                var result = await UserSearch.SearchAsync(query, page, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (_authentication.CurrentUser?.VrchatUserId != account) return;
                foreach (var user in result.Users) _searchResults[user.VrchatUserId] = user;
                SearchPost(new { type = "users.results", requestId, users = result.Users, hasMore = result.HasMore });
            }
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_searchCancellation, cancellation) && _authentication.CurrentUser?.VrchatUserId == account)
                SearchPost(new { type = "users.error", requestId, message = "The lookup timed out. Please try again." });
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException)
        {
            if (!cancellation.IsCancellationRequested && _authentication.CurrentUser?.VrchatUserId == account)
                SearchPost(new { type = "users.error", requestId, message = ex is InvalidOperationException ? ex.Message : "VRChat search is unavailable. Please try again." });
        }
        finally { if (ReferenceEquals(_searchCancellation, cancellation)) _searchCancellation = null; }
    }
    // Search results stay in the desktop window; they never enter the instance/overlay roster.
    private void SearchPost(object message)
    {
        if (!_closing && _ready && Browser.CoreWebView2 is { } core && IsTrusted(core.Source))
            core.PostWebMessageAsJson(JsonSerializer.Serialize(message, _json));
    }
}
