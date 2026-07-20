using System.IO;
using ArasPatchUpgradeAssistant.Models;
using Serilog;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class DirectoryValidationService : IDirectoryValidationService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;

    public DirectoryValidationService(
        IFileSystem fileSystem,
        ILogger? logger = null)
    {
        _fileSystem = fileSystem;
        _logger = logger ?? Logger.None;
    }

    public DirectoryValidationSnapshot Validate(
        UpgradePathInfo paths,
        string generatedCmdPath)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (string.IsNullOrWhiteSpace(generatedCmdPath))
        {
            throw new ArgumentException(
                "Generated SETUP CMD path is required.",
                nameof(generatedCmdPath));
        }

        var patches = DetectPatchesBase(paths.SupportRoot);
        var solutions = DetectSolutionsBase(paths.SupportRoot);
        var descriptors = new[]
        {
            Folder("Support Root", paths.SupportRoot),
            Folder("commands", Path.Combine(paths.SupportRoot, "commands")),
            Folder("DBUpdateTool", Path.Combine(paths.SupportRoot, "tools", "DBUpdateTool")),
            Folder("consoleUpgrade", Path.Combine(
                paths.SupportRoot, "tools", "SolutionUpgrade", "consoleUpgrade")),
            Folder("Patches", patches.BasePath, forceMissing: !patches.Found),
            Folder("Core Pre Patches", Path.Combine(patches.BasePath, patches.CoreDirectoryName, "pre")),
            Folder("Core Post Patches", Path.Combine(patches.BasePath, patches.CoreDirectoryName, "post")),
            Folder("PE Pre Patches", Path.Combine(patches.BasePath, patches.PeDirectoryName, "pre")),
            Folder("PE Post Patches", Path.Combine(patches.BasePath, patches.PeDirectoryName, "post")),
            Folder("Solutions", solutions.BasePath, forceMissing: !solutions.Found),
            Folder("LOGS", Path.Combine(paths.SupportRoot, "LOGS"), allowEmpty: true),
            Folder("backup", Path.Combine(paths.SupportRoot, "backup"), allowEmpty: true),
            new Descriptor(
                "Generated SETUP CMD",
                DirectoryItemKind.File,
                Path.GetFullPath(generatedCmdPath),
                AllowEmpty: false)
        };

        return new DirectoryValidationSnapshot(
            DateTimeOffset.Now,
            descriptors.Select(ValidateItem).ToArray());
    }

    public void CreateDirectory(DirectoryValidationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != DirectoryItemKind.Folder)
        {
            throw new InvalidOperationException("Only Folder items can be created.");
        }

        try
        {
            _logger.Information(
                "Create directory started {DirectoryPath}",
                item.FullPath);
            if (!_fileSystem.DirectoryExists(item.FullPath))
            {
                _fileSystem.CreateDirectory(item.FullPath);
            }

            _logger.Information(
                "Create directory completed {DirectoryPath}",
                item.FullPath);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Error(
                exception,
                "Create directory failed {DirectoryPath}",
                item.FullPath);
            throw new IOException(ToUserFriendlyError(exception), exception);
        }
    }

    private DirectoryValidationItem ValidateItem(Descriptor descriptor)
    {
        try
        {
            if (descriptor.ForceMissing)
            {
                return new DirectoryValidationItem(
                    descriptor.Name,
                    descriptor.Kind,
                    descriptor.Path,
                    false,
                    DirectoryValidationStatus.Missing,
                    string.Empty,
                    descriptor.Kind == DirectoryItemKind.Folder);
            }

            var exists = descriptor.Kind == DirectoryItemKind.Folder
                ? _fileSystem.DirectoryExists(descriptor.Path)
                : _fileSystem.FileExists(descriptor.Path);

            if (!exists)
            {
                return new DirectoryValidationItem(
                    descriptor.Name,
                    descriptor.Kind,
                    descriptor.Path,
                    false,
                    DirectoryValidationStatus.Missing,
                    string.Empty,
                    descriptor.Kind == DirectoryItemKind.Folder);
            }

            return new DirectoryValidationItem(
                descriptor.Name,
                descriptor.Kind,
                descriptor.Path,
                true,
                DirectoryValidationStatus.OK,
                string.Empty,
                false);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return new DirectoryValidationItem(
                descriptor.Name,
                descriptor.Kind,
                descriptor.Path,
                false,
                DirectoryValidationStatus.Warning,
                ToUserFriendlyError(exception),
                false);
        }
    }

    private PatchesDetectionResult DetectPatchesBase(string supportRoot)
    {
        var patchesRoot = Path.Combine(supportRoot, "Patches");
        _logger.Information(
            "Detect patches base started {PatchesRoot}",
            patchesRoot);

        try
        {
            var candidates = EnumerateDirectoriesIncludingSelf(patchesRoot)
                .Select(CreatePatchesCandidate)
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .OrderByDescending(candidate => candidate.HasCore && candidate.HasPe)
                .ThenByDescending(candidate => candidate.HasCore)
                .ThenBy(candidate => GetPathDepth(patchesRoot, candidate.Path))
                .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _logger.Information(
                "Patches base candidates {Candidates}",
                string.Join("; ", candidates.Select(candidate => candidate.Path)));

            var selected = candidates.FirstOrDefault();
            var result = selected is null
                ? new PatchesDetectionResult(
                    false,
                    Path.GetFullPath(patchesRoot),
                    "core",
                    "PE")
                : new PatchesDetectionResult(
                    true,
                    selected.Path,
                    selected.CoreDirectoryName ?? "core",
                    selected.PeDirectoryName ?? "PE");

            _logger.Information(
                "Selected patches base {PatchesBase}",
                result.BasePath);
            _logger.Information(
                "Detect patches base completed {Found}",
                result.Found);
            return result;
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Error(
                exception,
                "Detect patches base failed {PatchesRoot}",
                patchesRoot);
            _logger.Information(
                "Patches base candidates {Candidates}",
                string.Empty);
            _logger.Information(
                "Selected patches base {PatchesBase}",
                patchesRoot);
            _logger.Information(
                "Detect patches base completed {Found}",
                false);
            return new PatchesDetectionResult(
                false,
                Path.GetFullPath(patchesRoot),
                "core",
                "PE");
        }
    }

    private SolutionsDetectionResult DetectSolutionsBase(string supportRoot)
    {
        var solutionsRoot = Path.Combine(supportRoot, "Solutions");
        _logger.Information(
            "Detect solutions base started {SolutionsRoot}",
            solutionsRoot);

        try
        {
            var candidates = EnumerateDirectoriesIncludingSelf(solutionsRoot)
                .Select(CreateSolutionsCandidate)
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .OrderByDescending(candidate => candidate.HasCoreImportsManifest)
                .ThenBy(candidate => GetPathDepth(solutionsRoot, candidate.Path))
                .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _logger.Information(
                "Solutions base candidates {Candidates}",
                string.Join("; ", candidates.Select(candidate => candidate.Path)));

            var selected = candidates.FirstOrDefault();
            var result = selected is null
                ? new SolutionsDetectionResult(false, Path.GetFullPath(solutionsRoot))
                : new SolutionsDetectionResult(true, selected.Path);

            _logger.Information(
                "Selected solutions base {SolutionsBase}",
                result.BasePath);
            _logger.Information(
                "Detect solutions base completed {Found}",
                result.Found);
            return result;
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Error(
                exception,
                "Detect solutions base failed {SolutionsRoot}",
                solutionsRoot);
            _logger.Information(
                "Solutions base candidates {Candidates}",
                string.Empty);
            _logger.Information(
                "Selected solutions base {SolutionsBase}",
                solutionsRoot);
            _logger.Information(
                "Detect solutions base completed {Found}",
                false);
            return new SolutionsDetectionResult(false, Path.GetFullPath(solutionsRoot));
        }
    }

    private PatchesCandidate? CreatePatchesCandidate(string path)
    {
        var childDirectories = GetDirectChildDirectories(path)
            .OrderBy(child => child, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var coreDirectory = childDirectories.FirstOrDefault(IsCoreDirectoryName);
        var peDirectory = childDirectories.FirstOrDefault(IsPeDirectoryName);

        return coreDirectory is null && peDirectory is null
            ? null
            : new PatchesCandidate(
                Path.GetFullPath(path),
                coreDirectory,
                peDirectory);
    }

    private SolutionsCandidate? CreateSolutionsCandidate(string path)
    {
        var manifestFiles = _fileSystem
            .EnumerateFileSystemEntries(path)
            .Where(entry => _fileSystem.FileExists(entry))
            .Where(entry => string.Equals(
                Path.GetExtension(entry),
                ".mf",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (manifestFiles.Length == 0)
        {
            return null;
        }

        return new SolutionsCandidate(
            Path.GetFullPath(path),
            manifestFiles.Any(file => string.Equals(
                Path.GetFileName(file),
                "core_imports.mf",
                StringComparison.OrdinalIgnoreCase)));
    }

    private IEnumerable<string> EnumerateDirectoriesIncludingSelf(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!_fileSystem.DirectoryExists(fullRoot))
        {
            return [];
        }

        return EnumerateDirectoriesRecursive(fullRoot).Prepend(fullRoot);
    }

    private IEnumerable<string> EnumerateDirectoriesRecursive(string path)
    {
        foreach (var childDirectory in GetDirectChildDirectories(path))
        {
            yield return childDirectory;

            foreach (var nestedDirectory in EnumerateDirectoriesRecursive(childDirectory))
            {
                yield return nestedDirectory;
            }
        }
    }

    private IEnumerable<string> GetDirectChildDirectories(string path) =>
        _fileSystem
            .EnumerateFileSystemEntries(path)
            .Where(entry => _fileSystem.DirectoryExists(entry))
            .Select(Path.GetFullPath);

    private static bool IsCoreDirectoryName(string path) =>
        (Path.GetFileName(path) ?? string.Empty).Contains(
            "core",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsPeDirectoryName(string path) =>
        (Path.GetFileName(path) ?? string.Empty).Contains(
            "PE",
            StringComparison.OrdinalIgnoreCase);

    private static int GetPathDepth(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        return string.Equals(relativePath, ".", StringComparison.Ordinal)
            ? 0
            : relativePath
                .Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries)
                .Length;
    }

    private static Descriptor Folder(
        string name,
        string path,
        bool allowEmpty = false,
        bool forceMissing = false) =>
        new(
            name,
            DirectoryItemKind.Folder,
            Path.GetFullPath(path),
            allowEmpty,
            forceMissing);

    private static bool IsPathException(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or ArgumentException
            or NotSupportedException;

    private static string ToUserFriendlyError(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Access denied.",
        ArgumentException or NotSupportedException => "Invalid path.",
        IOException => $"I/O error while reading or creating path: {exception.Message}",
        _ => $"Unexpected path error: {exception.Message}"
    };

    private sealed record Descriptor(
        string Name,
        DirectoryItemKind Kind,
        string Path,
        bool AllowEmpty,
        bool ForceMissing = false);

    private sealed record PatchesDetectionResult(
        bool Found,
        string BasePath,
        string CoreDirectoryName,
        string PeDirectoryName);

    private sealed record SolutionsDetectionResult(bool Found, string BasePath);

    private sealed record PatchesCandidate(
        string Path,
        string? CoreDirectoryPath,
        string? PeDirectoryPath)
    {
        public bool HasCore => CoreDirectoryPath is not null;

        public bool HasPe => PeDirectoryPath is not null;

        public string? CoreDirectoryName =>
            CoreDirectoryPath is null ? null : System.IO.Path.GetFileName(CoreDirectoryPath);

        public string? PeDirectoryName =>
            PeDirectoryPath is null ? null : System.IO.Path.GetFileName(PeDirectoryPath);
    }

    private sealed record SolutionsCandidate(
        string Path,
        bool HasCoreImportsManifest);
}
