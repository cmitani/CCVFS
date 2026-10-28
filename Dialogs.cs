using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

public static class Dialogs
{
    private static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    // ── Message boxes ───────────────────────────────────────────────

    public static Task ShowError(Window owner, string message) =>
        ShowMessage(owner, "Ошибка", message);

    public static Task ShowInfo(Window owner, string title, string message) =>
        ShowMessage(owner, title, message);

    public static Task ShowWarning(Window owner, string title, string message) =>
        ShowMessage(owner, title, message);

    private static Task ShowMessage(Window owner, string title, string message)
    {
        var (win, _) = BuildBox<object>(title, message, ("OK", 0));
        return win.ShowDialog(owner);
    }

    public static Task<bool> ShowQuestion(Window owner, string message, string title)
    {
        var (win, result) = BuildBox<bool>(title, message, ("Да", true), ("Нет", false));
        win.ShowDialog(owner);
        return result;
    }

    // ── Input dialog (container size etc.) ──────────────────────────

    public static Task<string?> ShowInput(Window owner, string prompt, string title, string def = "")
    {
        var tcs = new TaskCompletionSource<string?>();

        var box = new TextBox { Text = def, Width = 324 };

        var ok = new Button { Content = "OK", MinWidth = 75, IsDefault = true };
        var cancel = new Button { Content = "Отмена", MinWidth = 75, IsCancel = true };

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { ok, cancel }
        });

        var win = new Window
        {
            Title = title,
            Content = panel,
            SizeToContent = SizeToContent.Height,
            Width = 400,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        ok.Click += (_, _) => { tcs.TrySetResult(box.Text?.Trim()); win.Close(); };
        cancel.Click += (_, _) => { tcs.TrySetResult(null); win.Close(); };
        win.Closed += (_, _) => tcs.TrySetResult(null);
        win.Opened += (_, _) => { box.Focus(); box.SelectAll(); };

        win.ShowDialog(owner);
        return tcs.Task;
    }

    private static (Window Win, Task<T> Result) BuildBox<T>(string title, string text, params (string Label, T Value)[] options)
    {
        var tcs = new TaskCompletionSource<T>();

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var win = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            MaxWidth = 520,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        foreach (var (label, value) in options)
        {
            var btn = new Button { Content = label, MinWidth = 88 };
            btn.Click += (_, _) => { tcs.TrySetResult(value); win.Close(); };
            row.Children.Add(btn);
        }

        // закрытие окна крестником без выбора кнопки — возвращаем default
        win.Closed += (_, _) => tcs.TrySetResult(default!);

        var panel = new StackPanel { Margin = new Thickness(24), MinWidth = 300, Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(row);

        win.Content = panel;
        return (win, tcs.Task);
    }

    // ── File / folder pickers ───────────────────────────────────────

    public static async Task<string?> PickFolderAsync(Window owner)
    {
        var picked = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выбрать папку для контейнера",
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetStartLocation(owner),
        });
        return picked.Count > 0 ? ToPath(picked[0]) : null;
    }

    public static async Task<string?> PickOpenFileAsync(Window owner, string title)
    {
        var picked = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetStartLocation(owner),
            FileTypeFilter = new List<FilePickerFileType> { AllFiles() },
        });
        return picked.Count > 0 ? ToPath(picked[0]) : null;
    }

    public static async Task<string?> PickSaveFileAsync(Window owner, string title, string fileName)
    {
        var picked = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = fileName,
            SuggestedStartLocation = await TryGetStartLocation(owner),
            FileTypeChoices = new List<FilePickerFileType> { AllFiles() },
        });
        return picked != null ? ToPath(picked) : null;
    }

    private static FilePickerFileType AllFiles() =>
        new("Все файлы") { Patterns = new[] { "*.*" } };

    private static string? ToPath(IStorageItem item) =>
        item.TryGetLocalPath() ?? item.Path.ToString();

    private static async Task<IStorageFolder?> TryGetStartLocation(Window owner)
    {
        try { return await owner.StorageProvider.TryGetFolderFromPathAsync(Desktop); }
        catch { return null; }
    }
}
