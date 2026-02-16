using DofusManager.Core.Helpers;
using Xunit;

namespace DofusManager.Tests.Helpers;

public class GlobMatcherTests
{
    [Fact]
    public void Star_MatchesEverything()
    {
        Assert.True(GlobMatcher.IsMatch("*", "anything"));
        Assert.True(GlobMatcher.IsMatch("*", ""));
        Assert.True(GlobMatcher.IsMatch("*", "Dofus - Panda"));
    }

    [Fact]
    public void StarPrefix_MatchesSuffix()
    {
        Assert.True(GlobMatcher.IsMatch("*Panda*", "Dofus - Panda-Main"));
        Assert.True(GlobMatcher.IsMatch("*Panda*", "Panda"));
        Assert.False(GlobMatcher.IsMatch("*Panda*", "Dofus - Iop"));
    }

    [Fact]
    public void ExactMatch_Works()
    {
        Assert.True(GlobMatcher.IsMatch("Dofus", "Dofus"));
        Assert.False(GlobMatcher.IsMatch("Dofus", "Dofus - Panda"));
    }

    [Fact]
    public void CaseInsensitive()
    {
        Assert.True(GlobMatcher.IsMatch("*panda*", "Dofus - PANDA"));
        Assert.True(GlobMatcher.IsMatch("*PANDA*", "dofus - panda"));
    }

    [Fact]
    public void QuestionMark_MatchesSingleChar()
    {
        Assert.True(GlobMatcher.IsMatch("Dofus - ?anda", "Dofus - Panda"));
        Assert.False(GlobMatcher.IsMatch("Dofus - ?anda", "Dofus - anda"));
    }

    [Fact]
    public void ComplexPattern()
    {
        Assert.True(GlobMatcher.IsMatch("Dofus*Panda*", "Dofus - Panda-Main"));
        Assert.False(GlobMatcher.IsMatch("Dofus*Panda*", "Retro - Panda-Main"));
    }
}
