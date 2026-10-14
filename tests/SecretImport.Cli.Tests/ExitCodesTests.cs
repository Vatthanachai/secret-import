using SecretImport.Core;

namespace SecretImport.Cli.Tests;

public class ExitCodesTests
{
    [Fact]
    public void Codes_MatchAdr0008()
    {
        Assert.Equal(0, ExitCodes.Success);
        Assert.Equal(1, ExitCodes.InvalidInput);
        Assert.Equal(2, ExitCodes.Cancelled);
        Assert.Equal(3, ExitCodes.EnvironmentError);
    }

    [Theory]
    [InlineData(ImportErrorKind.InvalidInput, 1)]
    [InlineData(ImportErrorKind.NoProjectFound, 1)]
    [InlineData(ImportErrorKind.MultipleProjects, 1)]
    [InlineData(ImportErrorKind.SdkUnavailable, 3)]
    [InlineData(ImportErrorKind.ConcurrentModification, 3)]
    [InlineData(ImportErrorKind.IoFailure, 3)]
    public void FromErrorKind_MapsEveryKind(ImportErrorKind kind, int expected)
    {
        Assert.Equal(expected, ExitCodes.FromErrorKind(kind));
    }

    [Fact]
    public void FromErrorKind_RejectsAnUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExitCodes.FromErrorKind((ImportErrorKind)999));
    }
}
