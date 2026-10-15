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

    [Fact]
    public void ChangesPending_IsFour_PerAdr0014()
    {
        Assert.Equal(4, ExitCodes.ChangesPending);
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

    [Theory]
    [InlineData(CliErrorKind.InvalidInput, 1)]
    [InlineData(CliErrorKind.NoProjectFound, 1)]
    [InlineData(CliErrorKind.MultipleProjects, 1)]
    [InlineData(CliErrorKind.InvalidOptions, 1)]
    [InlineData(CliErrorKind.ConfirmationRequired, 2)]
    [InlineData(CliErrorKind.Cancelled, 2)]
    [InlineData(CliErrorKind.SdkUnavailable, 3)]
    [InlineData(CliErrorKind.ConcurrentModification, 3)]
    [InlineData(CliErrorKind.IoFailure, 3)]
    public void FromCliErrorKind_MapsEveryKind(CliErrorKind kind, int expected)
    {
        Assert.Equal(expected, ExitCodes.FromErrorKind(kind));
    }

    [Fact]
    public void FromCliErrorKind_RejectsAnUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExitCodes.FromErrorKind((CliErrorKind)999));
    }

    [Theory]
    [InlineData(ImportErrorKind.InvalidInput, CliErrorKind.InvalidInput)]
    [InlineData(ImportErrorKind.NoProjectFound, CliErrorKind.NoProjectFound)]
    [InlineData(ImportErrorKind.MultipleProjects, CliErrorKind.MultipleProjects)]
    [InlineData(ImportErrorKind.SdkUnavailable, CliErrorKind.SdkUnavailable)]
    [InlineData(ImportErrorKind.ConcurrentModification, CliErrorKind.ConcurrentModification)]
    [InlineData(ImportErrorKind.IoFailure, CliErrorKind.IoFailure)]
    public void ToCliErrorKind_KeepsEveryCoreKind(ImportErrorKind kind, CliErrorKind expected)
    {
        Assert.Equal(expected, kind.ToCliErrorKind());
    }

    [Fact]
    public void ToCliErrorKind_RejectsAnUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ImportErrorKind)999).ToCliErrorKind());
    }
}
