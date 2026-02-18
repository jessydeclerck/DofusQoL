using DofusManager.Core.Models;
using DofusManager.Core.Win32;
using Serilog;

namespace DofusManager.Core.Services;

public class GroupInviteService : IGroupInviteService
{
    private static readonly ILogger Logger = Log.ForContext<GroupInviteService>();
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_SPACE = 0x20;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const ushort VK_W = 0x57;
    private const int DelayBetweenInvites = 300;
    private const int DelayBetweenFollows = 200;
    private const int FocusDelayMs = 100;
    private const int FocusMaxRetries = 3;

    private readonly IWin32WindowHelper _windowHelper;

    public GroupInviteService(IWin32WindowHelper windowHelper)
    {
        _windowHelper = windowHelper;
    }

    public async Task<GroupInviteResult> InviteAllAsync(IReadOnlyList<DofusWindow> windows, DofusWindow leader)
    {
        if (windows.Count <= 1)
        {
            return new GroupInviteResult { Invited = 0, Success = true };
        }

        var leaderName = IGroupInviteService.ExtractCharacterName(leader.Title);
        if (string.IsNullOrEmpty(leaderName))
        {
            return new GroupInviteResult
            {
                Invited = 0, Success = false,
                ErrorMessage = "Impossible d'extraire le nom du leader"
            };
        }

        // Focus la fenêtre du leader avec retries
        var focused = false;
        for (var attempt = 1; attempt <= FocusMaxRetries; attempt++)
        {
            _windowHelper.FocusWindow(leader.Handle);
            await Task.Delay(FocusDelayMs);

            if (_windowHelper.GetForegroundWindow() == leader.Handle)
            {
                focused = true;
                Logger.Information("[GROUP-INVITE] Focus leader OK (tentative {Attempt})", attempt);
                break;
            }

            Logger.Warning("[GROUP-INVITE] Focus leader échoué (tentative {Attempt}/{Max})",
                attempt, FocusMaxRetries);
        }

        if (!focused)
        {
            return new GroupInviteResult
            {
                Invited = 0, Success = false,
                ErrorMessage = "Le focus sur la fenêtre leader a échoué après plusieurs tentatives"
            };
        }

        var invited = 0;

        foreach (var window in windows)
        {
            if (window.Handle == leader.Handle)
                continue;

            var name = IGroupInviteService.ExtractCharacterName(window.Title);
            if (string.IsNullOrEmpty(name))
            {
                Logger.Warning("Impossible d'extraire le nom depuis le titre '{Title}'", window.Title);
                continue;
            }

            Logger.Information("[GROUP-INVITE] /invite {Name}", name);

            // ESPACE pour ouvrir le chat
            _windowHelper.SendKeyPress(VK_SPACE);
            await Task.Delay(50);

            // Taper /invite <nom>
            _windowHelper.SendText($"/invite {name}");
            await Task.Delay(50);

            // ENTER pour envoyer
            _windowHelper.SendKeyPress(VK_RETURN);

            invited++;

            // Délai entre les invites
            if (invited < windows.Count - 1)
                await Task.Delay(DelayBetweenInvites);
        }

        Logger.Information("[GROUP-INVITE] {Invited} personnage(s) invité(s)", invited);

        return new GroupInviteResult { Invited = invited, Success = true };
    }

    public async Task<GroupInviteResult> ToggleAutoFollowAsync(IReadOnlyList<DofusWindow> windows, DofusWindow leader)
    {
        if (windows.Count <= 1)
        {
            return new GroupInviteResult { Invited = 0, Success = true };
        }

        var toggled = 0;

        foreach (var window in windows)
        {
            if (window.Handle == leader.Handle)
                continue;

            // Focus la fenêtre cible avec retries
            var focused = false;
            for (var attempt = 1; attempt <= FocusMaxRetries; attempt++)
            {
                _windowHelper.FocusWindow(window.Handle);
                await Task.Delay(FocusDelayMs);

                if (_windowHelper.GetForegroundWindow() == window.Handle)
                {
                    focused = true;
                    break;
                }

                Logger.Warning("[AUTOFOLLOW] Focus échoué pour {Handle} (tentative {Attempt}/{Max})",
                    window.Handle, attempt, FocusMaxRetries);
            }

            if (!focused)
            {
                Logger.Warning("[AUTOFOLLOW] Impossible de focus {Handle}, skip", window.Handle);
                continue;
            }

            // Ctrl+W pour toggle autofollow
            _windowHelper.SendKeyCombination(VK_CONTROL, VK_W);
            toggled++;

            Logger.Information("[AUTOFOLLOW] Ctrl+W envoyé à {Handle}", window.Handle);

            await Task.Delay(DelayBetweenFollows);
        }

        // Restaurer le focus sur le leader
        _windowHelper.FocusWindow(leader.Handle);

        Logger.Information("[AUTOFOLLOW] {Count} fenêtre(s) toggled", toggled);

        return new GroupInviteResult { Invited = toggled, Success = true };
    }

    public async Task<GroupInviteResult> PasteToChatAsync(
        IReadOnlyList<DofusWindow> windows, DofusWindow? leader, bool doubleEnter = false)
    {
        if (windows.Count == 0)
            return new GroupInviteResult { Success = false, ErrorMessage = "Aucune fenêtre" };

        var pasted = 0;

        foreach (var window in windows)
        {
            var focused = await FocusWithRetryAsync(window.Handle);
            if (!focused)
            {
                Logger.Warning("[PASTE-CHAT] Focus échoué pour {Handle}, skip", window.Handle);
                continue;
            }

            // ESPACE → ouvre le chat
            _windowHelper.SendKeyPress(VK_SPACE);
            await Task.Delay(50);

            // Ctrl+V → colle le contenu du presse-papier
            _windowHelper.SendKeyCombination(VK_CONTROL, VK_V);
            await Task.Delay(50);

            // ENTRÉE → envoie le message
            _windowHelper.SendKeyPress(VK_RETURN);
            await Task.Delay(200);

            // 2e ENTRÉE pour confirmer (ex: actions nécessitant validation)
            if (doubleEnter)
            {
                _windowHelper.SendKeyPress(VK_RETURN);
                await Task.Delay(200);
            }

            pasted++;
            Logger.Information("[PASTE-CHAT] Collé dans {Handle}", window.Handle);
        }

        // Restaurer le focus au leader
        if (leader is not null)
            await FocusWithRetryAsync(leader.Handle);

        Logger.Information("[PASTE-CHAT] {Count} fenêtre(s) collées", pasted);
        return new GroupInviteResult { Success = true, Invited = pasted };
    }

    private async Task<bool> FocusWithRetryAsync(nint handle)
    {
        for (var attempt = 1; attempt <= FocusMaxRetries; attempt++)
        {
            _windowHelper.FocusWindow(handle);
            await Task.Delay(FocusDelayMs);

            if (_windowHelper.GetForegroundWindow() == handle)
                return true;
        }

        return false;
    }
}
