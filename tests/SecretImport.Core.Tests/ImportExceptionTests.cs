namespace SecretImport.Core.Tests;

public class ImportExceptionTests
{
    [Fact]
    public void Constructor_SetsKindAndMessage_AndDefaultsCandidatesToEmpty()
    {
        ImportException ex = new(ImportErrorKind.NoProjectFound, "No project found.");

        Assert.Equal(ImportErrorKind.NoProjectFound, ex.Kind);
        Assert.Equal("No project found.", ex.Message);
        Assert.Empty(ex.Candidates);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Constructor_KeepsCandidates()
    {
        ProjectRef[] candidates = [new ProjectRef("a/A.csproj", "A"), new ProjectRef("b/B.csproj", "B")];

        ImportException ex = new(ImportErrorKind.MultipleProjects, "Multiple projects.", candidates);

        Assert.Equal(candidates, ex.Candidates);
    }

    [Fact]
    public void Constructor_KeepsInnerException()
    {
        IOException inner = new("disk");

        ImportException ex = new(ImportErrorKind.IoFailure, "Write failed.", innerException: inner);

        Assert.Same(inner, ex.InnerException);
        Assert.Empty(ex.Candidates);
    }
}
