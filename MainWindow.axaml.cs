using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CCVFS;

public partial class MainWindow : Window
{
    private readonly ContainerManager _manager = new ContainerManager();
    private ContainerSession? _session;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ── Properties ───────────────────────────────────────────────────

    private string ContainerPath
    {
        get
        {
            string folder = (FolderBox.Text ?? "").Trim();
            string name   = (NameBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return "";
            if (!name.EndsWith(".ccvfs", StringComparison.OrdinalIgnoreCase))
                name += ".ccvfs";
            return string.IsNullOrEmpty(folder) ? name : Path.Combine(folder, name);
        }
    }

    private string Password => PwdBox.Text ?? "";

    private string? KeyfilePath =>
        KeyfileCheck.IsChecked == true && !string.IsNullOrWhiteSpace(KeyfileBox.Text)
            ? KeyfileBox.Text.Trim() : null;

    // ── Helpers ───────────────────────────────────────────────────────

    private void SetStatus(string msg) => StatusLabel.Text = "Статус: " + msg;

    private void SetBusy(bool busy)
    {
        foreach (var btn in new[] { CreateBtn, OpenBtn, AddBtn, ExtractBtn, DeleteBtn, IntegrityBtn })
            btn.IsEnabled = !busy;
        BusyBar.IsVisible = busy;
        Title = busy
            ? "CCVFS — выполняется операция, подождите…"
            : "CCVFS — Зашифрованный контейнер";
    }

    private void RefreshFileList()
    {
        FileList.Items.Clear();
        if (_session == null) return;
        foreach (var e in _session.Files)
        {
            string size = e.OriginalSize >= 1024 * 1024
                ? $"{e.OriginalSize / (1024.0 * 1024):F2} МБ"
                : $"{e.OriginalSize / 1024.0:F1} КБ";
            FileList.Items.Add($"{e.Name,-40} {size,10}");
        }
    }

    private async Task Err(string msg) => await Dialogs.ShowError(this, msg);

    // ── Auto-split full path pasted into folder box ───────────────────

    private void FolderBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        string text = (FolderBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(text)) return;

        // If user pasted a full file path (has extension or is an existing file), split it
        string ext = Path.GetExtension(text);
        if (!string.IsNullOrEmpty(ext) || File.Exists(text))
        {
            FolderBox.Text = Path.GetDirectoryName(text) ?? text;
            if (string.IsNullOrEmpty((NameBox.Text ?? "").Trim()))
                NameBox.Text = Path.GetFileNameWithoutExtension(text);
        }
    }

    // ── Event handlers ────────────────────────────────────────────────

    private void KeyfileCheck_Changed(object? sender, RoutedEventArgs e)
    {
        bool on = KeyfileCheck.IsChecked == true;
        KeyfileBox.IsEnabled = on;
        BrowseKeyfileBtn.IsEnabled = on;
        if (!on) KeyfileBox.Clear();
    }

    private async void BrowseFolder_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await Dialogs.PickFolderAsync(this);
        if (path != null) FolderBox.Text = path;
    }

    private async void BrowseKeyfile_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await Dialogs.PickOpenFileAsync(this, "Выбрать ключевой файл");
        if (path != null) KeyfileBox.Text = path;
    }

    private async void CreateBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ContainerPath)) { await Err("Укажите папку и имя контейнера."); return; }
        if (string.IsNullOrEmpty(Password))      { await Err("Введите пароль."); return; }

        string? sizeStr = await Dialogs.ShowInput(this, "Размер контейнера (МБ):", "Создать контейнер", "100");
        if (sizeStr == null) return;
        if (!int.TryParse(sizeStr, out int sizeMb) || sizeMb <= 0)
        {
            await Err("Введите целое число больше нуля.");
            return;
        }

        SetBusy(true);
        SetStatus($"Создание контейнера {sizeMb} МБ, подождите…");
        try
        {
            string path = ContainerPath, pass = Password;
            string? kf  = KeyfilePath;
            await Task.Run(() => _manager.Create(path, sizeMb, pass, kf));
            _session = await Task.Run(() => _manager.Open(path, pass, kf));
            RefreshFileList();
            SetStatus($"Создан: {path}");
        }
        catch (Exception ex) { await Err(ex.Message); SetStatus("Ошибка при создании."); }
        finally { SetBusy(false); }
    }

    private async void OpenBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ContainerPath)) { await Err("Укажите папку и имя контейнера."); return; }
        if (string.IsNullOrEmpty(Password))      { await Err("Введите пароль."); return; }

        SetBusy(true);
        SetStatus("Открытие…");
        try
        {
            string path = ContainerPath, pass = Password;
            string? kf  = KeyfilePath;
            _session = await Task.Run(() => _manager.Open(path, pass, kf));
            RefreshFileList();
            SetStatus($"Открыт: {path}  |  файлов: {_session.Files.Count}");
        }
        catch (Exception ex) { await Err(ex.Message); SetStatus("Ошибка при открытии."); }
        finally { SetBusy(false); }
    }

    private async void AddBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ContainerPath)) { await Err("Укажите папку и имя контейнера."); return; }
        if (string.IsNullOrEmpty(Password))      { await Err("Введите пароль."); return; }

        string? filePath = await Dialogs.PickOpenFileAsync(this, "Выбрать файл");
        if (filePath == null) return;

        SetBusy(true);
        SetStatus($"Добавление «{Path.GetFileName(filePath)}»…");
        try
        {
            string src = filePath, path = ContainerPath, pass = Password;
            string? kf = KeyfilePath;
            await Task.Run(() => _manager.AddFile(path, pass, kf, src));
            _session = await Task.Run(() => _manager.Open(path, pass, kf));
            RefreshFileList();
            SetStatus($"Добавлен: {Path.GetFileName(src)}  |  файлов: {_session.Files.Count}");
        }
        catch (Exception ex) { await Err(ex.Message); SetStatus("Ошибка при добавлении."); }
        finally { SetBusy(false); }
    }

    private async void ExtractBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null)                          { await Err("Сначала откройте контейнер."); return; }
        if (FileList.SelectedIndex < 0) { await Err("Выберите файл из списка."); return; }

        string fileName = _session.Files[FileList.SelectedIndex].Name;

        string? destPath = await Dialogs.PickSaveFileAsync(this, "Сохранить как…", fileName);
        if (destPath == null) return;

        SetBusy(true);
        SetStatus($"Извлечение «{fileName}»…");
        try
        {
            string dest = destPath, path = ContainerPath, pass = Password;
            string? kf  = KeyfilePath;
            await Task.Run(() => _manager.ExtractFile(path, pass, kf, fileName, dest));
            SetStatus($"Извлечён: {dest}");
        }
        catch (Exception ex) { await Err(ex.Message); SetStatus("Ошибка при извлечении."); }
        finally { SetBusy(false); }
    }

    private async void DeleteBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null)                          { await Err("Сначала откройте контейнер."); return; }
        if (FileList.SelectedIndex < 0) { await Err("Выберите файл из списка."); return; }

        string fileName = _session.Files[FileList.SelectedIndex].Name;

        bool confirm = await Dialogs.ShowQuestion(this, $"Удалить «{fileName}» из контейнера?", "Подтверждение");
        if (!confirm) return;

        SetBusy(true);
        SetStatus($"Удаление «{fileName}»…");
        try
        {
            string path = ContainerPath, pass = Password;
            string? kf  = KeyfilePath;
            await Task.Run(() => _manager.DeleteFile(path, pass, kf, fileName));
            _session = await Task.Run(() => _manager.Open(path, pass, kf));
            RefreshFileList();
            SetStatus($"Удалён: {fileName}");
        }
        catch (Exception ex) { await Err(ex.Message); SetStatus("Ошибка при удалении."); }
        finally { SetBusy(false); }
    }

    private async void IntegrityBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ContainerPath)) { await Err("Укажите папку и имя контейнера."); return; }
        if (string.IsNullOrEmpty(Password))      { await Err("Введите пароль."); return; }

        SetBusy(true);
        SetStatus("Проверка целостности…");
        try
        {
            string path = ContainerPath, pass = Password;
            string? kf  = KeyfilePath;
            await Task.Run(() => _manager.Open(path, pass, kf));
            SetStatus("Целостность подтверждена — HMAC совпадает.");
            await Dialogs.ShowInfo(this, "Проверка пройдена", "HMAC совпадает. Контейнер не повреждён.");
        }
        catch (Exception ex)
        {
            SetStatus("Проверка не пройдена: " + ex.Message);
            await Dialogs.ShowWarning(this, "Целостность нарушена", ex.Message);
        }
        finally { SetBusy(false); }
    }
}
