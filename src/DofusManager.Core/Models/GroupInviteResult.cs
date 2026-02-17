namespace DofusManager.Core.Models;

public class GroupInviteResult
{
    public int Invited { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
