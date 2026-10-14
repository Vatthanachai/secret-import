namespace SecretImport.Core.Tests;

public sealed class ImportPlannerTests
{
    private readonly ImportPlanner _planner = new();

    private static Dictionary<string, string> D(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.OrdinalIgnoreCase);

    private static SecretChange Change(ImportPlan plan, string key) => Assert.Single(plan.Changes, c => c.Key == key);

    [Fact]
    public void Overwrite_ClassifiesAddedChangedUnchanged_AndKeepsExistingOnlyKeys()
    {
        Dictionary<string, string> existing = D(("Same", "1"), ("Diff", "old"), ("Kept", "k"));
        Dictionary<string, string> incoming = D(("Same", "1"), ("Diff", "new"), ("New", "n"));

        ImportPlan plan = _planner.Plan(existing, incoming, ImportMode.Overwrite);

        Assert.Equal(new SecretChange("Same", SecretChangeKind.Unchanged, "1", "1"), Change(plan, "Same"));
        Assert.Equal(new SecretChange("Diff", SecretChangeKind.Changed, "old", "new"), Change(plan, "Diff"));
        Assert.Equal(new SecretChange("New", SecretChangeKind.Added, null, "n"), Change(plan, "New"));
        Assert.DoesNotContain(plan.Changes, c => c.Key == "Kept");
        Assert.Equal(3, plan.Changes.Count);
        Assert.Equal(["Same", "Diff", "Kept", "New"], plan.Result.Keys);
        Assert.Equal("new", plan.Result["Diff"]);
        Assert.Equal("k", plan.Result["Kept"]);
        Assert.True(plan.HasChanges);
    }

    [Fact]
    public void Skip_KeepsExistingValues_AndMarksConflictsAsSkipped()
    {
        Dictionary<string, string> existing = D(("Same", "1"), ("Diff", "old"), ("Kept", "k"));
        Dictionary<string, string> incoming = D(("Same", "1"), ("Diff", "new"), ("New", "n"));

        ImportPlan plan = _planner.Plan(existing, incoming, ImportMode.Skip);

        Assert.Equal(new SecretChange("Same", SecretChangeKind.Unchanged, "1", "1"), Change(plan, "Same"));
        Assert.Equal(new SecretChange("Diff", SecretChangeKind.Skipped, "old", "new"), Change(plan, "Diff"));
        Assert.Equal(new SecretChange("New", SecretChangeKind.Added, null, "n"), Change(plan, "New"));
        Assert.Equal("old", plan.Result["Diff"]);
        Assert.Equal("k", plan.Result["Kept"]);
        Assert.Equal(4, plan.Result.Count);
    }

    [Fact]
    public void Replace_RemovesExistingKeysThatAreNotInTheImport()
    {
        Dictionary<string, string> existing = D(("Same", "1"), ("Diff", "old"), ("Gone", "g"));
        Dictionary<string, string> incoming = D(("New", "n"), ("Diff", "new"), ("Same", "1"));

        ImportPlan plan = _planner.Plan(existing, incoming, ImportMode.Replace);

        Assert.Equal(new SecretChange("Gone", SecretChangeKind.Removed, "g", null), Change(plan, "Gone"));
        Assert.Equal(SecretChangeKind.Changed, Change(plan, "Diff").Kind);
        Assert.Equal(SecretChangeKind.Added, Change(plan, "New").Kind);
        Assert.Equal(SecretChangeKind.Unchanged, Change(plan, "Same").Kind);
        Assert.Equal(["New", "Diff", "Same"], plan.Result.Keys);
        Assert.False(plan.Result.ContainsKey("Gone"));
    }

    [Fact]
    public void Changes_AreSortedByKey()
    {
        ImportPlan plan = _planner.Plan(D(("b", "x")), D(("C", "1"), ("a", "2"), ("B", "y")), ImportMode.Overwrite);

        Assert.Equal(["a", "b", "C"], plan.Changes.Select(static c => c.Key));
    }

    [Fact]
    public void KeysDifferingOnlyByCase_AreTheSameKey_AndKeepTheExistingSpelling()
    {
        ImportPlan plan = _planner.Plan(D(("ConnStr", "old")), D(("CONNSTR", "new")), ImportMode.Overwrite);

        SecretChange change = Assert.Single(plan.Changes);
        Assert.Equal(new SecretChange("ConnStr", SecretChangeKind.Changed, "old", "new"), change);
        Assert.Equal(["ConnStr"], plan.Result.Keys);
    }

    [Fact]
    public void ValuesAreComparedCaseSensitively()
    {
        ImportPlan plan = _planner.Plan(D(("K", "abc")), D(("K", "ABC")), ImportMode.Overwrite);

        Assert.Equal(SecretChangeKind.Changed, Assert.Single(plan.Changes).Kind);
    }

    [Fact]
    public void Result_IsCaseInsensitive()
    {
        ImportPlan plan = _planner.Plan(D(), D(("Key", "v")), ImportMode.Overwrite);

        Assert.Equal("v", plan.Result["KEY"]);
    }

    [Fact]
    public void ExistingDictionary_WithOrdinalComparer_IsStillMatchedCaseInsensitively()
    {
        Dictionary<string, string> existing = new(StringComparer.Ordinal) { ["Key"] = "v" };

        ImportPlan plan = _planner.Plan(existing, D(("KEY", "v")), ImportMode.Replace);

        Assert.Equal(new SecretChange("Key", SecretChangeKind.Unchanged, "v", "v"), Assert.Single(plan.Changes));
    }

    [Theory]
    [InlineData(ImportMode.Overwrite)]
    [InlineData(ImportMode.Skip)]
    [InlineData(ImportMode.Replace)]
    public void IdenticalInput_HasNoChanges(ImportMode mode)
    {
        ImportPlan plan = _planner.Plan(D(("A", "1"), ("B", "2")), D(("A", "1"), ("B", "2")), mode);

        Assert.False(plan.HasChanges);
        Assert.All(plan.Changes, static c => Assert.Equal(SecretChangeKind.Unchanged, c.Kind));
    }

    [Fact]
    public void Skip_WithOnlyConflicts_HasNoChanges()
    {
        ImportPlan plan = _planner.Plan(D(("A", "1")), D(("A", "2")), ImportMode.Skip);

        Assert.False(plan.HasChanges);
        Assert.Equal(SecretChangeKind.Skipped, Assert.Single(plan.Changes).Kind);
    }

    [Fact]
    public void Overwrite_WithEmptyIncoming_HasNoChanges_AndKeepsEverything()
    {
        ImportPlan plan = _planner.Plan(D(("A", "1")), D(), ImportMode.Overwrite);

        Assert.False(plan.HasChanges);
        Assert.Empty(plan.Changes);
        Assert.Equal("1", plan.Result["A"]);
    }

    [Fact]
    public void Replace_WithEmptyIncoming_RemovesEverything()
    {
        ImportPlan plan = _planner.Plan(D(("A", "1")), D(), ImportMode.Replace);

        Assert.True(plan.HasChanges);
        Assert.Empty(plan.Result);
    }

    [Fact]
    public void UnknownMode_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _planner.Plan(D(), D(), (ImportMode)42));
    }
}