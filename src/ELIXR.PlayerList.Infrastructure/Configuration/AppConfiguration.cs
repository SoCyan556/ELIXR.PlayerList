namespace ELIXR.PlayerList.Infrastructure.Configuration;
public sealed class AppConfiguration
{
    public string VrchatApiBaseUrl { get; init; } = "https://api.vrchat.cloud/api/1";
    public string ElixrApiBaseUrl { get; init; } = "";
    public string GitHubRepository { get; init; } = "SoCyan556/ELIXR.PlayerList";
    public string UserAgent { get; init; } = "ELIXR Player List/0.1.0 (contact: CONFIGURE_ME)";
}
