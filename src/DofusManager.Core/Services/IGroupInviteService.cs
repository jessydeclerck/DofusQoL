using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

public interface IGroupInviteService
{
    /// <summary>
    /// Touche utilisée pour ouvrir le chat in-game (défaut : VK_SPACE = 0x20).
    /// </summary>
    ushort ChatOpenKeyCode { get; set; }

    Task<GroupInviteResult> InviteAllAsync(IReadOnlyList<DofusWindow> windows, DofusWindow leader);

    Task<GroupInviteResult> ToggleAutoFollowAsync(IReadOnlyList<DofusWindow> windows, DofusWindow leader);

    Task<GroupInviteResult> PasteToChatAsync(IReadOnlyList<DofusWindow> windows, DofusWindow? leader, bool doubleEnter = false, int doubleEnterDelayMs = 500);

    static string ExtractCharacterName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var dashIndex = title.IndexOf(" - ", StringComparison.Ordinal);
        return dashIndex > 0 ? title[..dashIndex].Trim() : title.Trim();
    }
}
