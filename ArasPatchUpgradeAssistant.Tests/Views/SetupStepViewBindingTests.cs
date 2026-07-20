using System.Xml.Linq;

namespace ArasPatchUpgradeAssistant.Tests.Views;

public sealed class SetupStepViewBindingTests
{
    [Fact]
    public void ReadOnlyTextBoxes_UseExplicitOneWayTextBindings()
    {
        var document = XDocument.Load(FindSetupStepView());
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var invalidBindings = document
            .Descendants(presentation + "TextBox")
            .Where(element =>
                string.Equals(
                    element.Attribute("IsReadOnly")?.Value,
                    "True",
                    StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Attribute("Text")?.Value)
            .Where(binding => binding?.StartsWith("{Binding", StringComparison.Ordinal) is true)
            .Where(binding =>
                !binding!.Contains("Mode=OneWay", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(invalidBindings);
    }

    [Fact]
    public void ReadOnlyOperationalInfoFields_AreDisplayedInSetupStepView()
    {
        var document = XDocument.Load(FindSetupStepView());
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var textBindings = document
            .Descendants(presentation + "TextBox")
            .Select(element => element.Attribute("Text")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains(textBindings, binding => binding!.Contains("ReferencePath", StringComparison.Ordinal));
        Assert.Contains(textBindings, binding => binding!.Contains("VaultConfigPath", StringComparison.Ordinal));
        Assert.Contains(textBindings, binding => binding!.Contains("SettingsFilePath", StringComparison.Ordinal));
        Assert.Contains(textBindings, binding => binding!.Contains("LogDirectory", StringComparison.Ordinal));
        Assert.Contains(textBindings, binding => binding!.Contains("SelectedSqlServer", StringComparison.Ordinal));
        Assert.Contains(textBindings, binding => binding!.Contains("SelectedDatabaseName", StringComparison.Ordinal));
        Assert.Contains(textBindings, binding => binding!.Contains("CopySourceDbName", StringComparison.Ordinal));
        Assert.Contains(textBindings, binding => binding!.Contains("SqlLoginName", StringComparison.Ordinal));
    }

    [Fact]
    public void SetupSections_AreExpandableAndBoundToSectionFlags()
    {
        var document = XDocument.Load(FindSetupStepView());
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var expanderBindings = document
            .Descendants(presentation + "Expander")
            .Select(element => element.Attribute("IsExpanded")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains(expanderBindings, binding => binding!.Contains("IsSetupPathSectionExpanded", StringComparison.Ordinal));
        Assert.Contains(expanderBindings, binding => binding!.Contains("IsInnovatorSectionExpanded", StringComparison.Ordinal));
        Assert.Contains(expanderBindings, binding => binding!.Contains("IsSqlLoginSectionExpanded", StringComparison.Ordinal));
        Assert.Contains(expanderBindings, binding => binding!.Contains("IsPreviewSectionExpanded", StringComparison.Ordinal));
        Assert.Contains(expanderBindings, binding => binding!.Contains("IsResultSectionExpanded", StringComparison.Ordinal));
    }

    [Fact]
    public void PreviewGrid_DisplaysOriginalValueWriteValueAndStatus()
    {
        var document = XDocument.Load(FindSetupStepView());
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var columnBindings = document
            .Descendants(presentation + "DataGridTextColumn")
            .Select(element => element.Attribute("Binding")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains(columnBindings, binding => binding!.Contains("Name", StringComparison.Ordinal));
        Assert.Contains(columnBindings, binding => binding!.Contains("OriginalValue", StringComparison.Ordinal));
        Assert.Contains(columnBindings, binding => binding!.Contains("Value", StringComparison.Ordinal));
        Assert.Contains(columnBindings, binding => binding!.Contains("Status", StringComparison.Ordinal));

        var textBindings = document
            .Descendants(presentation + "TextBlock")
            .Select(element => element.Attribute("Text")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains(textBindings, binding => binding!.Contains("PreviewWarningMessage", StringComparison.Ordinal));
    }

    private static string FindSetupStepView()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "ArasPatchUpgradeAssistant",
                "Views",
                "SetupStepView.xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "找不到 ArasPatchUpgradeAssistant/Views/SetupStepView.xaml。");
    }
}
