using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Tests.TestDoubles;
using ArasPatchUpgradeAssistant.ViewModels;

namespace ArasPatchUpgradeAssistant.Tests.ViewModels;

public sealed class DirectoryValidationStepViewModelTests
{
    [Fact]
    public void Refresh_ReplacesItemsAndUpdatesCounts()
    {
        var fixture = new Fixture();
        fixture.Validation.Items =
        [
            fixture.Item("ok", DirectoryValidationStatus.OK),
            fixture.Item("missing", DirectoryValidationStatus.Missing),
            fixture.Item("warning", DirectoryValidationStatus.Warning)
        ];
        var vm = fixture.CreateViewModel();

        vm.RefreshCommand.Execute(null);

        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(1, vm.OkCount);
        Assert.Equal(1, vm.MissingCount);
        Assert.Equal(1, vm.WarningCount);
        Assert.NotNull(vm.LastCheckedAt);
    }

    [Fact]
    public void CreateFolder_WhenConfirmed_CreatesAndRefreshes()
    {
        var fixture = new Fixture();
        var missing = fixture.Item(
            "missing",
            DirectoryValidationStatus.Missing,
            canCreate: true);
        fixture.Validation.Items = [missing];
        var vm = fixture.CreateViewModel();

        vm.CreateFolderCommand.Execute(missing);

        Assert.Contains(missing.FullPath, fixture.Validation.CreatedPaths);
        Assert.Equal(1, fixture.Validation.ValidateCallCount);
        Assert.Contains(missing.FullPath, fixture.Messages.Confirmations.Single());
    }

    [Fact]
    public void CreateFolder_WhenDeclined_DoesNothing()
    {
        var fixture = new Fixture();
        fixture.Messages.ConfirmResult = false;
        var missing = fixture.Item(
            "missing",
            DirectoryValidationStatus.Missing,
            canCreate: true);
        var vm = fixture.CreateViewModel();

        vm.CreateFolderCommand.Execute(missing);

        Assert.Empty(fixture.Validation.CreatedPaths);
        Assert.Equal(0, fixture.Validation.ValidateCallCount);
    }

    private sealed class Fixture
    {
        private readonly UpgradePathInfo _paths = new(
            @"K:\Upgrade\12SP18\Support\commands\01\SETUP-DEFAULTS-MACHINENAME.CMD",
            "01",
            @"K:\Upgrade\12SP18\Support",
            @"K:\Upgrade\12SP18",
            "12SP18",
            "120");

        public FakeDirectoryValidationService Validation { get; } = new();
        public FakeMessageDialogService Messages { get; } = new();

        public DirectoryValidationStepViewModel CreateViewModel()
        {
            var vm = new DirectoryValidationStepViewModel(Validation, Messages);
            vm.Configure(_paths, @"K:\Upgrade\12SP18\Support\commands\01\SETUP-DEFAULTS-BUILD01.CMD");
            return vm;
        }

        public DirectoryValidationItem Item(
            string name,
            DirectoryValidationStatus status,
            bool canCreate = false) =>
            new(
                name,
                DirectoryItemKind.Folder,
                Path.Combine(_paths.SupportRoot, name),
                status != DirectoryValidationStatus.Missing,
                status,
                string.Empty,
                canCreate);
    }
}
