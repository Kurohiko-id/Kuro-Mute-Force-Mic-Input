using System.Net.Http;
using System.Text.Json;

namespace MuteMic;

internal static class UpdateChecker
{
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/Kurohiko-id/Kuro-Mute-Force-Mic-Input/releases/latest";

    public readonly record struct UpdateInfo(Version Version, string ReleaseUrl);

    // Returns null on no update, no releases yet, or any failure (offline, rate-limited, ...) —
    // this check is best-effort and must never surface an error to the user.
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MuteMic-UpdateChecker");

            using var response = await http.GetAsync(LatestReleaseApiUrl);
            if (!response.IsSuccessStatusCode)
                return null;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
            var url = json.RootElement.GetProperty("html_url").GetString();

            if (tag is null || url is null || !Version.TryParse(tag, out var latest))
                return null;

            var current = typeof(UpdateChecker).Assembly.GetName().Version;
            return current is not null && latest > current ? new UpdateInfo(latest, url) : null;
        }
        catch
        {
            return null;
        }
    }
}
