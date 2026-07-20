using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestDoubles;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class DirectoryValidationServiceTests
{
    private readonly string _supportRoot = Path.Combine("K:\\", "Upgrade", "12SP18", "Support");
    private readonly string _generatedCmd;
    private readonly UpgradePathInfo _paths;

    public DirectoryValidationServiceTests()
    {
        _generatedCmd = Path.Combine(_supportRoot, "commands", "01", "SETUP-DEFAULTS-BUILD01.CMD");
        _paths = new UpgradePathInfo(
            Path.Combine(_supportRoot, "commands", "01", SetupPathParser.ExpectedFilename),
            "01",
            _supportRoot,
            Directory.GetParent(_supportRoot)!.FullName,
            "12SP18",
            "120");
    }

    [Fact]
    public void Validate_ReturnsTwelveFoldersAndGeneratedFileInFixedOrder()
    {
        var snapshot = new DirectoryValidationService(new FakeFileSystem())
            .Validate(_paths, _generatedCmd);

        Assert.Equal(13, snapshot.Items.Count);
        Assert.Equal(
            [
                "Support Root", "commands", "DBUpdateTool", "consoleUpgrade",
                "Patches", "Core Pre Patches", "Core Post Patches",
                "PE Pre Patches", "PE Post Patches", "Solutions",
                "LOGS", "backup", "Generated SETUP CMD"
            ],
            snapshot.Items.Select(item => item.Name));
        Assert.All(snapshot.Items.Take(12),
            item => Assert.Equal(DirectoryItemKind.Folder, item.Kind));
        Assert.Equal(DirectoryItemKind.File, snapshot.Items[12].Kind);
        Assert.Equal(_generatedCmd, snapshot.Items[12].FullPath);
    }

    [Fact]
    public void Validate_WhenNoPatchesOrSolutionsBaseFound_UsesRootFallback()
    {
        var items = new DirectoryValidationService(new FakeFileSystem())
            .Validate(_paths, _generatedCmd).Items;

        Assert.Equal(Path.Combine(_supportRoot, "commands"), items[1].FullPath);
        Assert.Equal(
            Path.Combine(_supportRoot, "tools", "DBUpdateTool"),
            items[2].FullPath);
        Assert.Equal(
            Path.Combine(_supportRoot, "Patches", "core", "pre"),
            items[5].FullPath);
        Assert.Equal(
            Path.Combine(_supportRoot, "Solutions"),
            items[9].FullPath);
    }

    [Fact]
    public void Validate_DetectsR38PatchesAndSolutionsBase()
    {
        var fileSystem = new FakeFileSystem();
        var patchesRoot = Path.Combine(_supportRoot, "Patches");
        var core = Path.Combine(patchesRoot, "core");
        var pe = Path.Combine(patchesRoot, "PE");
        var solutionsRoot = Path.Combine(_supportRoot, "Solutions");
        var imports = Path.Combine(solutionsRoot, "core_imports.mf");
        fileSystem.AddDirectory(patchesRoot, core, pe);
        fileSystem.AddDirectory(core);
        fileSystem.AddDirectory(pe);
        fileSystem.AddDirectory(solutionsRoot, imports);
        fileSystem.AddFile(imports);

        var items = new DirectoryValidationService(fileSystem)
            .Validate(_paths, _generatedCmd).Items;

        Assert.Equal(patchesRoot, items.Single(item => item.Name == "Patches").FullPath);
        Assert.Equal(
            Path.Combine(patchesRoot, "core", "pre"),
            items.Single(item => item.Name == "Core Pre Patches").FullPath);
        Assert.Equal(
            Path.Combine(patchesRoot, "core", "post"),
            items.Single(item => item.Name == "Core Post Patches").FullPath);
        Assert.Equal(
            Path.Combine(patchesRoot, "PE", "pre"),
            items.Single(item => item.Name == "PE Pre Patches").FullPath);
        Assert.Equal(
            Path.Combine(patchesRoot, "PE", "post"),
            items.Single(item => item.Name == "PE Post Patches").FullPath);
        Assert.Equal(solutionsRoot, items.Single(item => item.Name == "Solutions").FullPath);
    }

    [Fact]
    public void Validate_DetectsNestedPatchesAndSolutionsBase()
    {
        var fileSystem = new FakeFileSystem();
        var patchesRoot = Path.Combine(_supportRoot, "Patches");
        var patches120 = Path.Combine(patchesRoot, "120");
        var core = Path.Combine(patches120, "core");
        var pe = Path.Combine(patches120, "PE");
        var solutionsRoot = Path.Combine(_supportRoot, "Solutions");
        var solutions120 = Path.Combine(solutionsRoot, "120");
        var imports = Path.Combine(solutions120, "core_imports.mf");
        fileSystem.AddDirectory(patchesRoot, patches120);
        fileSystem.AddDirectory(patches120, core, pe);
        fileSystem.AddDirectory(core);
        fileSystem.AddDirectory(pe);
        fileSystem.AddDirectory(solutionsRoot, solutions120);
        fileSystem.AddDirectory(solutions120, imports);
        fileSystem.AddFile(imports);

        var items = new DirectoryValidationService(fileSystem)
            .Validate(_paths, _generatedCmd).Items;

        Assert.Equal(patches120, items.Single(item => item.Name == "Patches").FullPath);
        Assert.Equal(
            Path.Combine(patches120, "core", "pre"),
            items.Single(item => item.Name == "Core Pre Patches").FullPath);
        Assert.Equal(
            Path.Combine(patches120, "PE", "post"),
            items.Single(item => item.Name == "PE Post Patches").FullPath);
        Assert.Equal(solutions120, items.Single(item => item.Name == "Solutions").FullPath);
    }

    [Fact]
    public void Validate_MissingItems_AreMissingAndOnlyFoldersCanBeCreated()
    {
        var items = new DirectoryValidationService(new FakeFileSystem())
            .Validate(_paths, _generatedCmd).Items;

        Assert.All(items.Take(12), item =>
        {
            Assert.Equal(DirectoryValidationStatus.Missing, item.Status);
            Assert.True(item.CanCreate);
        });
        Assert.Equal(DirectoryValidationStatus.Missing, items[12].Status);
        Assert.False(items[12].CanCreate);
    }

    [Fact]
    public void Validate_ExistingEmptyFoldersAreOk()
    {
        var fileSystem = new FakeFileSystem();
        AddAllDirectories(fileSystem, nonEmpty: false);
        fileSystem.AddFile(_generatedCmd);

        var items = new DirectoryValidationService(fileSystem)
            .Validate(_paths, _generatedCmd).Items;

        Assert.Equal(DirectoryValidationStatus.OK, items[0].Status);
        Assert.Equal(DirectoryValidationStatus.OK, items[10].Status);
        Assert.Equal(DirectoryValidationStatus.OK, items[11].Status);
        Assert.Equal(DirectoryValidationStatus.OK, items[12].Status);
    }

    [Fact]
    public void Validate_NonEmptyNormalFoldersAreOk()
    {
        var fileSystem = new FakeFileSystem();
        AddAllDirectories(fileSystem, nonEmpty: true);

        var items = new DirectoryValidationService(fileSystem)
            .Validate(_paths, _generatedCmd).Items;

        Assert.All(items.Take(12),
            item => Assert.Equal(DirectoryValidationStatus.OK, item.Status));
    }

    [Fact]
    public void Validate_OneAccessErrorWarnsAndOtherItemsContinue()
    {
        var fileSystem = new FakeFileSystem();
        AddAllDirectories(fileSystem, nonEmpty: true);
        var commandsPath = Path.Combine(_supportRoot, "commands");
        fileSystem.ThrowWhenCheckingDirectory(
            commandsPath,
            new UnauthorizedAccessException("denied"));

        var items = new DirectoryValidationService(fileSystem)
            .Validate(_paths, _generatedCmd).Items;

        Assert.Equal(DirectoryValidationStatus.Warning, items[1].Status);
        Assert.Contains("Access denied", items[1].ErrorMessage);
        Assert.Equal(DirectoryValidationStatus.OK, items[2].Status);
    }

    [Fact]
    public void CreateDirectory_CreatesMissingFolderAndIsIdempotent()
    {
        var fileSystem = new FakeFileSystem();
        var service = new DirectoryValidationService(fileSystem);
        var folder = new DirectoryValidationItem(
            "LOGS",
            DirectoryItemKind.Folder,
            Path.Combine(_supportRoot, "LOGS"),
            false,
            DirectoryValidationStatus.Missing,
            string.Empty,
            true);

        service.CreateDirectory(folder);
        service.CreateDirectory(folder);

        Assert.Single(fileSystem.CreatedDirectories);
        Assert.Contains(folder.FullPath, fileSystem.CreatedDirectories);
    }

    [Fact]
    public void CreateDirectory_RejectsFileItem()
    {
        var file = new DirectoryValidationItem(
            "Generated SETUP CMD",
            DirectoryItemKind.File,
            _generatedCmd,
            false,
            DirectoryValidationStatus.Missing,
            string.Empty,
            false);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new DirectoryValidationService(new FakeFileSystem())
                .CreateDirectory(file));

        Assert.Contains("Folder", exception.Message);
    }

    private void AddAllDirectories(FakeFileSystem fileSystem, bool nonEmpty)
    {
        var patchesRoot = Path.Combine(_supportRoot, "Patches");
        var core = Path.Combine(patchesRoot, "core");
        var pe = Path.Combine(patchesRoot, "PE");
        var solutionsRoot = Path.Combine(_supportRoot, "Solutions");
        var imports = Path.Combine(solutionsRoot, "core_imports.mf");
        fileSystem.AddDirectory(_supportRoot, Entries(_supportRoot));
        fileSystem.AddDirectory(
            Path.Combine(_supportRoot, "commands"),
            Entries(Path.Combine(_supportRoot, "commands")));
        fileSystem.AddDirectory(
            Path.Combine(_supportRoot, "tools", "DBUpdateTool"),
            Entries(Path.Combine(_supportRoot, "tools", "DBUpdateTool")));
        fileSystem.AddDirectory(
            Path.Combine(_supportRoot, "tools", "SolutionUpgrade", "consoleUpgrade"),
            Entries(Path.Combine(_supportRoot, "tools", "SolutionUpgrade", "consoleUpgrade")));
        fileSystem.AddDirectory(patchesRoot, core, pe);
        fileSystem.AddDirectory(core, Path.Combine(core, "pre"), Path.Combine(core, "post"));
        fileSystem.AddDirectory(pe, Path.Combine(pe, "pre"), Path.Combine(pe, "post"));
        fileSystem.AddDirectory(Path.Combine(core, "pre"), Entries(Path.Combine(core, "pre")));
        fileSystem.AddDirectory(Path.Combine(core, "post"), Entries(Path.Combine(core, "post")));
        fileSystem.AddDirectory(Path.Combine(pe, "pre"), Entries(Path.Combine(pe, "pre")));
        fileSystem.AddDirectory(Path.Combine(pe, "post"), Entries(Path.Combine(pe, "post")));
        fileSystem.AddDirectory(solutionsRoot, imports);
        fileSystem.AddFile(imports);
        fileSystem.AddDirectory(Path.Combine(_supportRoot, "LOGS"));
        fileSystem.AddDirectory(Path.Combine(_supportRoot, "backup"));

        string[] Entries(string path)
        {
            return nonEmpty ? [Path.Combine(path, "entry")] : [];
        }
    }
}
