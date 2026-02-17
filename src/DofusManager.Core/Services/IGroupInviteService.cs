using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

public interface IGroupInviteService
{
    Task<GroupInviteResult> InviteAllAsync(IReadOnlyList<DofusWindow> windows, DofusWindow leader);

    Task<GroupInviteResult> ToggleAutoFollowAsync(IReadOnlyList<DofusWindow> windows, DofusWindow leader);

    static string ExtractCharacterName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var dashIndex = title.IndexOf(" - ", StringComparison.Ordinal);
        return dashIndex > 0 ? title[..dashIndex].Trim() : title.Trim();
    }
}
