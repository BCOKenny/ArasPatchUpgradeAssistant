using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArasPatchUpgradeAssistant.ViewModels;

public partial class BatExecutionPlanItemViewModel : ObservableObject
{
    private bool _isUpdatingCheckState;

    public event Action<BatExecutionPlanItemViewModel>? CheckStateChanged;

    public event Action<BatExecutionPlanItemViewModel, BatUpdateItemViewModel>? ChildCheckStateChanged;

    public ObservableCollection<BatUpdateItemViewModel> Updates { get; } = [];

    [ObservableProperty]
    private bool? _isChecked = false;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private int _order;

    public string FileName { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int ChildItemCount => Updates.Count;

    public bool HasChildren => Updates.Count > 0;

    public bool IsExternal { get; init; }

    public string ExternalId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string OriginalFilePath { get; init; } = string.Empty;

    public string StoredRelativePath { get; init; } = string.Empty;

    public string StoredFullPath { get; init; } = string.Empty;

    public string FileExtension { get; init; } = string.Empty;

    public string CatalogPath { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public void AddUpdate(BatUpdateItemViewModel update)
    {
        InsertUpdate(Updates.Count, update);
    }

    public void InsertUpdate(int index, BatUpdateItemViewModel update)
    {
        if (index < 0 || index > Updates.Count)
        {
            index = Updates.Count;
        }

        if (index == Updates.Count)
        {
            Updates.Add(update);
        }
        else
        {
            Updates.Insert(index, update);
        }

        update.CheckStateChanged += OnChildCheckStateChanged;
        NotifyChildrenChanged();
    }

    public void RemoveUpdate(BatUpdateItemViewModel update)
    {
        update.CheckStateChanged -= OnChildCheckStateChanged;
        Updates.Remove(update);
        NotifyChildrenChanged();
        RefreshCheckStateFromChildren();
    }

    public void MoveUpdate(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 ||
            oldIndex >= Updates.Count ||
            newIndex < 0 ||
            newIndex >= Updates.Count ||
            oldIndex == newIndex)
        {
            return;
        }

        Updates.Move(oldIndex, newIndex);
        NotifyChildrenChanged();
    }

    public string GetPlanKey() =>
        IsExternal && !string.IsNullOrWhiteSpace(ExternalId)
            ? ExternalId
            : FileName;

    [RelayCommand]
    private void ToggleExpanded()
    {
        if (HasChildren)
        {
            IsExpanded = !IsExpanded;
        }
    }

    public void RefreshCheckStateFromChildren()
    {
        if (Updates.Count == 0)
        {
            if (IsChecked is null)
            {
                SetParentCheckState(false);
            }

            return;
        }

        SetParentCheckState(GetCheckStateFromChildren());
    }

    partial void OnIsCheckedChanged(bool? value)
    {
        if (_isUpdatingCheckState)
        {
            return;
        }

        if (Updates.Count == 0 && value is null)
        {
            SetParentCheckState(false);
            CheckStateChanged?.Invoke(this);
            return;
        }

        if (value.HasValue)
        {
            _isUpdatingCheckState = true;
            foreach (var update in Updates)
            {
                update.IsChecked = value.Value;
            }

            _isUpdatingCheckState = false;
        }

        CheckStateChanged?.Invoke(this);
    }

    private void OnChildCheckStateChanged(BatUpdateItemViewModel update)
    {
        if (_isUpdatingCheckState)
        {
            return;
        }

        RefreshCheckStateFromChildren();
        ChildCheckStateChanged?.Invoke(this, update);
    }

    private bool? GetCheckStateFromChildren()
    {
        if (Updates.All(update => update.IsChecked))
        {
            return true;
        }

        return Updates.Any(update => update.IsChecked)
            ? null
            : false;
    }

    private void SetParentCheckState(bool? value)
    {
        _isUpdatingCheckState = true;
        IsChecked = value;
        _isUpdatingCheckState = false;
    }

    private void NotifyChildrenChanged()
    {
        OnPropertyChanged(nameof(ChildItemCount));
        OnPropertyChanged(nameof(HasChildren));
    }
}
