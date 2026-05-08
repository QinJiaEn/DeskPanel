using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DeskPanel.Models;
using DeskPanel.Services;

namespace DeskPanel.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private ObservableCollection<Category> _categories = new();
    private ObservableCollection<FileEntry> _files = new();
    private ObservableCollection<FileEntry> _filteredFiles = new();
    private Category? _selectedCategory;
    private string _searchText = "";
    private string _statusText = "";

    public ObservableCollection<Category> Categories
    {
        get => _categories;
        set { _categories = value; OnPropertyChanged(); }
    }

    public ObservableCollection<FileEntry> Files
    {
        get => _files;
        set { _files = value; OnPropertyChanged(); }
    }

    public ObservableCollection<FileEntry> FilteredFiles
    {
        get => _filteredFiles;
        set { _filteredFiles = value; OnPropertyChanged(); }
    }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public ICommand SelectAllCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand AddCategoryCommand { get; }
    public ICommand EditCategoryCommand { get; }
    public ICommand DeleteCategoryCommand { get; }
    public ICommand CollectDesktopCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand DeleteFileCommand { get; }
    public ICommand CopyPathCommand { get; }

    public MainViewModel()
    {
        SelectAllCommand = new RelayCommand(_ => SelectAll());
        SelectCategoryCommand = new RelayCommand(param =>
        {
            if (param is Category cat) SelectedCategory = cat;
        });
        AddCategoryCommand = new RelayCommand(_ => AddCategory());
        EditCategoryCommand = new RelayCommand(param =>
        {
            if (param is Category cat) EditCategory(cat);
        });
        DeleteCategoryCommand = new RelayCommand(param =>
        {
            if (param is Category cat) DeleteCategory(cat);
        });
        CollectDesktopCommand = new RelayCommand(_ => CollectDesktop());
        OpenFileCommand = new RelayCommand(param =>
        {
            if (param is FileEntry entry) FileOperationService.OpenFile(entry);
        });
        DeleteFileCommand = new RelayCommand(param =>
        {
            if (param is FileEntry entry) DeleteFile(entry);
        });
        CopyPathCommand = new RelayCommand(param =>
        {
            if (param is FileEntry entry) System.Windows.Clipboard.SetText(entry.StoredPath);
        });
    }

    public void LoadData()
    {
        var categories = DataService.GetCategories();
        var allFiles = DataService.GetFiles();

        // Update file counts
        foreach (var cat in categories)
            cat.FileCount = allFiles.Count(f => f.CategoryId == cat.Id);

        Categories = new ObservableCollection<Category>(categories);
        Files = new ObservableCollection<FileEntry>(allFiles);
        ApplyFilter();
    }

    private void SelectAll()
    {
        SelectedCategory = null;
    }

    public void AddCategory(string? name = null, string? color = null)
    {
        var cat = new Category
        {
            Name = name ?? "新分类",
            Color = color ?? "#89b4fa",
            Order = Categories.Count
        };
        DataService.AddCategory(cat);
        Categories.Add(cat);
    }

    public void EditCategory(Category category)
    {
        DataService.SaveCategories(Categories.ToList());
    }

    public void DeleteCategory(Category category)
    {
        var result = System.Windows.MessageBox.Show(
            $"确定删除分类 \"{category.Name}\" 吗？\n该分类下的文件将移到第一个分类。",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        DataService.RemoveCategory(category.Id);
        Categories.Remove(category);
        SelectedCategory = null;
        LoadData();
    }

    public void CollectDesktop()
    {
        var cat = SelectedCategory ?? Categories.FirstOrDefault();
        if (cat == null)
        {
            System.Windows.MessageBox.Show("请先创建一个分类。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var files = DesktopCollectService.ScanDesktopFiles();
        if (files.Count == 0)
        {
            System.Windows.MessageBox.Show("桌面上没有可收纳的文件。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"将在桌面上找到 {files.Count} 个文件。\n收纳到分类: {cat.Name}\n\n确认继续？",
            "收纳桌面", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        var collectResult = DesktopCollectService.CollectDesktopFiles(cat);
        foreach (var entry in collectResult.NewEntries)
            DataService.AddFile(entry);

        LoadData();
        StatusText = $"收纳完成: {collectResult.SuccessCount} 成功";

        if (collectResult.FailCount > 0)
        {
            System.Windows.MessageBox.Show(
                $"收纳完成:\n成功: {collectResult.SuccessCount}\n失败: {collectResult.FailCount}\n\n错误:\n{string.Join("\n", collectResult.Errors.Take(5))}",
                "结果", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void AddFileFromDrop(string filePath, Category category)
    {
        try
        {
            var entry = FileOperationService.StoreFile(filePath, category);
            DataService.AddFile(entry);
            LoadData();
            StatusText = $"已添加: {entry.FileName}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"添加文件失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void DeleteFile(FileEntry entry)
    {
        try
        {
            FileOperationService.DeleteFile(entry);
            DataService.RemoveFile(entry.Id);
            Files.Remove(entry);
            ApplyFilter();
            StatusText = $"已删除: {entry.FileName}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"删除失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ApplyFilter()
    {
        var query = SearchText?.Trim().ToLower() ?? "";
        var filtered = Files.AsEnumerable();

        if (SelectedCategory != null)
            filtered = filtered.Where(f => f.CategoryId == SelectedCategory.Id);

        if (!string.IsNullOrEmpty(query))
            filtered = filtered.Where(f => f.FileName.ToLower().Contains(query));

        FilteredFiles = new ObservableCollection<FileEntry>(filtered);

        // Update file counts
        foreach (var cat in Categories)
            cat.FileCount = Files.Count(f => f.CategoryId == cat.Id);

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var allCount = Files.Count;
        var shownCount = FilteredFiles.Count;
        if (allCount == shownCount)
            StatusText = $"共 {allCount} 个文件";
        else
            StatusText = $"共 {allCount} 个文件，显示 {shownCount} 个";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
