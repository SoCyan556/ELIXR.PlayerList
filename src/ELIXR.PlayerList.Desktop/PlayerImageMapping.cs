using System.IO;
using ELIXR.PlayerList.Infrastructure.Services;
using Microsoft.Web.WebView2.Core;

namespace ELIXR.PlayerList.Desktop;

internal static class PlayerImageMapping
{
    internal static void Configure(CoreWebView2 core, string? directory)
    {
        if (directory is null) return;
        try
        {
            Directory.CreateDirectory(directory);
            // Cross-origin image display is allowed; script/fetch access remains denied.
            core.SetVirtualHostNameToFolderMapping(VrchatProfileImageService.ImageHost, directory, CoreWebView2HostResourceAccessKind.DenyCors);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Portraits fall back without preventing startup. */ }
    }
}
