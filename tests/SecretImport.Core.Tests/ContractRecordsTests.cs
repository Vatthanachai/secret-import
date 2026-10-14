namespace SecretImport.Core.Tests;

public class ContractRecordsTests
{
    private static readonly ProjectRef Api = new("src/Api/Api.csproj", "Api");

    [Fact]
    public void ProjectRef_HasValueEquality()
    {
        Assert.Equal(new ProjectRef("src/Api/Api.csproj", "Api"), Api);
        Assert.NotEqual(new ProjectRef("src/Web/Web.csproj", "Web"), Api);
    }

    [Fact]
    public void ImportAnalysis_KeepsEverythingItIsGiven()
    {
        ProjectRef worker = new("src/Worker/Worker.csproj", "Worker");
        ProjectInfo project = new(Api, "abc-123", SecretsIdSource.MsBuild, [worker]);
        ImportRequest request = new("src/Api", new JsonInput("in.json", "{}"), ImportMode.Replace, "Api");
        ImportPlan plan = new([new SecretChange("Key", SecretChangeKind.Added, null, "v")], new Dictionary<string, string> { ["Key"] = "v" });
        ImportWarning warning = new(ImportWarningKind.SharedSecretsId, "Shared with Worker.");

        ImportAnalysis analysis = new(project, request, "/home/u/secrets.json", NeedsInit: false, CanInit: true,
            ExistingCount: 2, IncomingCount: 1, plan, [warning], "hash");

        Assert.Same(project, analysis.Project);
        Assert.Equal(worker, Assert.Single(analysis.Project.SharedWith));
        Assert.Equal(SecretsIdSource.MsBuild, analysis.Project.Source);
        Assert.Same(request, analysis.Request);
        Assert.Equal(ImportMode.Replace, analysis.Request.Mode);
        Assert.Equal("Api", analysis.Request.Section);
        Assert.Equal("/home/u/secrets.json", analysis.SecretsPath);
        Assert.False(analysis.NeedsInit);
        Assert.True(analysis.CanInit);
        Assert.Equal(2, analysis.ExistingCount);
        Assert.Equal(1, analysis.IncomingCount);
        Assert.True(analysis.Plan.HasChanges);
        Assert.Equal(ImportWarningKind.SharedSecretsId, Assert.Single(analysis.Warnings).Kind);
        Assert.Equal("hash", analysis.ExistingFileHash);
    }

    [Fact]
    public void ImportResult_HasValueEquality()
    {
        ImportResult result = new(Added: 1, Changed: 2, Removed: 3, "/s/secrets.json", "/s/secrets.json.bak");

        Assert.Equal(new ImportResult(1, 2, 3, "/s/secrets.json", "/s/secrets.json.bak"), result);
        Assert.Equal(3, result.Removed);
        Assert.Equal("/s/secrets.json.bak", result.BackupPath);
    }
}
