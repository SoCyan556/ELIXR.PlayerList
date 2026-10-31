using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Desktop;
public partial class MainWindow
{
    internal RecentPlayersService? RecentPlayers { get; init; }
    private bool _recentDirty = true;
    private bool _recentFlushedBeforeClose;
    private void RecentChanged(object? sender, EventArgs e) { _recentDirty = true; PlayersChanged(this, _players.Current); }
    private async void FlushRecentBeforeClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_recentFlushedBeforeClose || RecentPlayers is null) return;
        e.Cancel = true;
        await RecentPlayers.FlushAsync();
        _recentFlushedBeforeClose = true;
        // An empty flush can finish synchronously inside Closing; defer the second Close.
        _ = Dispatcher.BeginInvoke(() => Close());
    }
}
