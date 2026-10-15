namespace SecretImport.Core.Tests;

public class ImportPlanTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyResult = new Dictionary<string, string>();

    [Fact]
    public void HasChanges_IsFalse_WhenThereAreNoChanges()
    {
        ImportPlan plan = new([], EmptyResult);

        Assert.False(plan.HasChanges);
    }

    [Theory]
    [InlineData(SecretChangeKind.Unchanged)]
    [InlineData(SecretChangeKind.Skipped)]
    public void HasChanges_IsFalse_WhenOnlyNonModifyingChanges(SecretChangeKind kind)
    {
        ImportPlan plan = new([new SecretChange("A", kind, "1", "1"), new SecretChange("B", kind, "2", "3")], EmptyResult);

        Assert.False(plan.HasChanges);
    }

    [Theory]
    [InlineData(SecretChangeKind.Added)]
    [InlineData(SecretChangeKind.Changed)]
    [InlineData(SecretChangeKind.Removed)]
    public void HasChanges_IsTrue_WhenAnyChangeModifiesTheStore(SecretChangeKind kind)
    {
        ImportPlan plan = new(
            [new SecretChange("A", SecretChangeKind.Unchanged, "1", "1"), new SecretChange("B", kind, "2", "3")],
            EmptyResult);

        Assert.True(plan.HasChanges);
    }
}
