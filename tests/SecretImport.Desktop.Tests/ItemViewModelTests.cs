using SecretImport.Core;
using SecretImport.Desktop.Tests.Fakes;
using SecretImport.Desktop.ViewModels;

namespace SecretImport.Desktop.Tests;

public class DiffRowViewModelTests
{
    [Theory]
    [InlineData(SecretChangeKind.Added, "+ added")]
    [InlineData(SecretChangeKind.Changed, "~ changed")]
    [InlineData(SecretChangeKind.Unchanged, "= unchanged")]
    [InlineData(SecretChangeKind.Removed, "- removed")]
    [InlineData(SecretChangeKind.Skipped, "skipped")]
    public void KindLabel_DescribesTheChange(SecretChangeKind kind, string label)
    {
        DiffRowViewModel row = new(new SecretChange("K", kind, "a", "b"));

        Assert.Equal(kind, row.Kind);
        Assert.Equal(label, row.KindLabel);
    }

    [Fact]
    public void Values_AreMaskedUntilRevealed()
    {
        DiffRowViewModel row = new(new SecretChange("Db:Password", SecretChangeKind.Changed, "old", "new"));

        Assert.Equal("Db:Password", row.Key);
        Assert.False(row.IsRevealed);
        Assert.Equal(DiffRowViewModel.Mask, row.OldValueDisplay);
        Assert.Equal(DiffRowViewModel.Mask, row.NewValueDisplay);
        Assert.Equal("Show", row.RevealLabel);
        Assert.True(row.HasValues);
    }

    [Fact]
    public void ToggleReveal_ShowsAndHidesTheValues()
    {
        DiffRowViewModel row = new(new SecretChange("K", SecretChangeKind.Changed, "old", "new"));
        List<string?> raised = [];
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.ToggleRevealCommand.Execute(null);

        Assert.True(row.IsRevealed);
        Assert.Equal("old", row.OldValueDisplay);
        Assert.Equal("new", row.NewValueDisplay);
        Assert.Equal("Hide", row.RevealLabel);
        Assert.Equal(
            [nameof(DiffRowViewModel.IsRevealed), nameof(DiffRowViewModel.OldValueDisplay), nameof(DiffRowViewModel.NewValueDisplay), nameof(DiffRowViewModel.RevealLabel)],
            raised);

        row.ToggleRevealCommand.Execute(null);

        Assert.False(row.IsRevealed);
        Assert.Equal(DiffRowViewModel.Mask, row.NewValueDisplay);
    }

    [Fact]
    public void MissingValues_AreShownEmpty()
    {
        DiffRowViewModel added = new(new SecretChange("A", SecretChangeKind.Added, null, "new"));
        DiffRowViewModel removed = new(new SecretChange("R", SecretChangeKind.Removed, "old", null));
        DiffRowViewModel empty = new(new SecretChange("E", SecretChangeKind.Unchanged, null, null));

        Assert.Equal(string.Empty, added.OldValueDisplay);
        Assert.Equal(string.Empty, removed.NewValueDisplay);
        Assert.True(added.HasValues);
        Assert.True(removed.HasValues);
        Assert.False(empty.HasValues);
    }
}

public class ProjectItemViewModelTests
{
    [Fact]
    public void Initialized_Project()
    {
        ProjectItemViewModel item = new(TestData.Project("App", "abc"));

        Assert.Equal("App", item.Name);
        Assert.Equal(Path.Combine("src", "App", "App.csproj"), item.Path);
        Assert.Equal("abc", item.UserSecretsId);
        Assert.Equal(ProjectStatus.Initialized, item.Status);
        Assert.Equal("✅", item.StatusGlyph);
        Assert.Equal("initialized", item.StatusText);
    }

    [Fact]
    public void NotInitialized_Project()
    {
        ProjectItemViewModel item = new(TestData.Project("App", null));

        Assert.Null(item.UserSecretsId);
        Assert.Equal(ProjectStatus.NotInitialized, item.Status);
        Assert.Equal("⚠️", item.StatusGlyph);
        Assert.Equal("not initialized", item.StatusText);
    }

    [Fact]
    public void Project_SharingItsId()
    {
        ProjectItemViewModel item = new(TestData.Project("App", "abc", new ProjectRef("b.csproj", "B"), new ProjectRef("c.csproj", "C")));

        Assert.Equal(ProjectStatus.SharedId, item.Status);
        Assert.Equal("\U0001F517", item.StatusGlyph);
        Assert.Equal("shares its UserSecretsId with B, C", item.StatusText);
    }
}
