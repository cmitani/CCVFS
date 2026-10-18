using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

public class MainForm : Form
{
    private readonly ContainerManager _manager = new ContainerManager();
    private ContainerSession? _session;

    private TextBox _folderBox = null!;
    private TextBox _nameBox   = null!;
    private TextBox _passBox   = null!;
    private CheckBox _keyfileCheck = null!;
    private TextBox _keyfileBox = null!;
    private Button _browseKeyfileBtn = null!;
    private ListBox _fileList = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;
    private Button _createBtn = null!;
    private Button _openBtn = null!;
    private Button _addBtn = null!;
    private Button _extractBtn = null!;
    private Button _deleteBtn = null!;
    private Button _integrityBtn = null!;

    public MainForm()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        Text = "CCVFS — Зашифрованный контейнер";
        Size = new Size(700, 540);
        MinimumSize = new Size(650, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        // ── Top panel ────────────────────────────────────────────────
        var top = new Panel { Dock = DockStyle.Top, Height = 148, Padding = new Padding(10) };

        int y = 10;

        // Row 1: Folder
        top.Controls.Add(new Label { Text = "Папка:", Location = new Point(10, y + 3), Width = 85, TextAlign = ContentAlignment.MiddleLeft });
        _folderBox = new TextBox { Location = new Point(100, y), Width = 470 };
        _folderBox.Leave += FolderBox_Leave;
        var browseFolder = new Button { Text = "...", Location = new Point(575, y), Width = 40 };
        browseFolder.Click += BrowseFolder_Click;
        top.Controls.AddRange(new Control[] { _folderBox, browseFolder });

        y += 33;

        // Row 2: Container name
        top.Controls.Add(new Label { Text = "Имя (.ccvfs):", Location = new Point(10, y + 3), Width = 85, TextAlign = ContentAlignment.MiddleLeft });
        _nameBox = new TextBox { Location = new Point(100, y), Width = 220 };
        top.Controls.Add(_nameBox);

        y += 33;

        // Row 3: Password
        top.Controls.Add(new Label { Text = "Пароль:", Location = new Point(10, y + 3), Width = 85, TextAlign = ContentAlignment.MiddleLeft });
        _passBox = new TextBox { Location = new Point(100, y), Width = 220, UseSystemPasswordChar = true };
        top.Controls.Add(_passBox);

        y += 33;

        // Row 4: Keyfile
        _keyfileCheck = new CheckBox { Text = "Keyfile:", Location = new Point(10, y + 2), Width = 85 };
        _keyfileBox = new TextBox { Location = new Point(100, y), Width = 415, Enabled = false };
        _browseKeyfileBtn = new Button { Text = "...", Location = new Point(520, y), Width = 40, Enabled = false };
        _keyfileCheck.CheckedChanged += (_, _) =>
        {
            _keyfileBox.Enabled = _keyfileCheck.Checked;
            _browseKeyfileBtn.Enabled = _keyfileCheck.Checked;
            if (!_keyfileCheck.Checked) _keyfileBox.Clear();
        };
        _browseKeyfileBtn.Click += BrowseKeyfile_Click;
        top.Controls.AddRange(new Control[] { _keyfileCheck, _keyfileBox, _browseKeyfileBtn });

        // ── Center (file list) ────────────────────────────────────────
        var center = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 4) };
        var listLabel = new Label { Text = "Файлы в контейнере:", Dock = DockStyle.Top, Height = 20 };
        _fileList = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };
        center.Controls.Add(_fileList);
        center.Controls.Add(listLabel);

        // ── Bottom panel ──────────────────────────────────────────────
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 110, Padding = new Padding(10) };

        int bx = 0, by = 8;
        _createBtn    = MakeBtn("Создать",   ref bx, by, 88);
        _openBtn      = MakeBtn("Открыть",   ref bx, by, 88);
        _addBtn       = MakeBtn("Добавить",  ref bx, by, 88);
        _extractBtn   = MakeBtn("Извлечь",   ref bx, by, 88);
        _deleteBtn    = MakeBtn("Удалить",   ref bx, by, 88);
        _integrityBtn = MakeBtn("Проверить", ref bx, by, 88);

        _createBtn.Click    += CreateBtn_Click;
        _openBtn.Click      += OpenBtn_Click;
        _addBtn.Click       += AddBtn_Click;
        _extractBtn.Click   += ExtractBtn_Click;
        _deleteBtn.Click    += DeleteBtn_Click;
        _integrityBtn.Click += IntegrityBtn_Click;

        by += 40;
        _statusLabel = new Label { Text = "Готов.", Location = new Point(0, by), Width = 660, Height = 22 };

        by += 28;
        _progressBar = new ProgressBar
        {
            Location = new Point(0, by), Width = 660, Height = 16,
            Style = ProgressBarStyle.Marquee, Visible = false
        };

        bottom.Controls.AddRange(new Control[]
        {
            _createBtn, _openBtn, _addBtn, _extractBtn, _deleteBtn, _integrityBtn,
            _statusLabel, _progressBar
        });

        Controls.Add(center);
        Controls.Add(top);
        Controls.Add(bottom);
    }

    private static Button MakeBtn(string text, ref int x, int y, int w)
    {
        var btn = new Button { Text = text, Location = new Point(x, y), Width = w };
        x += w + 5;
        return btn;
    }

    // ── Dialog helpers (STA thread) ───────────────────────────────────

    private static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    private static Task<string?> PickFolderAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        var t = new Thread(() =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Выбрать папку для контейнера",
                UseDescriptionForTitle = true,
                SelectedPath = Desktop
            };
            tcs.SetResult(dlg.ShowDialog() == DialogResult.OK ? dlg.SelectedPath : null);
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return tcs.Task;
    }

    private static Task<string?> PickOpenAsync(string title, string filter)
    {
        var tcs = new TaskCompletionSource<string?>();
        var t = new Thread(() =>
        {
            using var dlg = new OpenFileDialog { Title = title, Filter = filter, InitialDirectory = Desktop };
            tcs.SetResult(dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null);
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return tcs.Task;
    }

    private static Task<string?> PickSaveAsync(string title, string filter, string fileName = "")
    {
        var tcs = new TaskCompletionSource<string?>();
        var t = new Thread(() =>
        {
            using var dlg = new SaveFileDialog { Title = title, Filter = filter, FileName = fileName, InitialDirectory = Desktop };
            tcs.SetResult(dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null);
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return tcs.Task;
    }

    // ── Properties ───────────────────────────────────────────────────

    private string ContainerPath
    {
        get
        {
            string folder = _folderBox.Text.Trim();
            string name   = _nameBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return "";
            if (!name.EndsWith(".ccvfs", StringComparison.OrdinalIgnoreCase))
                name += ".ccvfs";
            return string.IsNullOrEmpty(folder) ? name : Path.Combine(folder, name);
        }
    }

    private string Password   => _passBox.Text;
    private string? KeyfilePath => _keyfileCheck.Checked && !string.IsNullOrWhiteSpace(_keyfileBox.Text)
                                   ? _keyfileBox.Text.Trim() : null;

    // ── Helpers ───────────────────────────────────────────────────────

    private void SetStatus(string msg) => _statusLabel.Text = "Статус: " + msg;

    private void SetBusy(bool busy)
    {
        foreach (var btn in new[] { _createBtn, _openBtn, _addBtn, _extractBtn, _deleteBtn, _integrityBtn })
            btn.Enabled = !busy;
        _progressBar.Visible = busy;
        Text = busy
            ? "CCVFS — выполняется операция, подождите…"
            : "CCVFS — Зашифрованный контейнер";
    }

    private void RefreshFileList()
    {
        _fileList.Items.Clear();
        if (_session == null) return;
        foreach (var e in _session.Files)
        {
            string size = e.OriginalSize >= 1024 * 1024
                ? $"{e.OriginalSize / (1024.0 * 1024):F2} МБ"
                : $"{e.OriginalSize / 1024.0:F1} КБ";
            _fileList.Items.Add($"{e.Name,-40} {size,10}");
        }
    }

    private string? AskInput(string prompt, string title, string def = "")
    {
        using var dlg = new Form
        {
            Text = title, Size = new Size(320, 130),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };
        var lbl  = new Label   { Text = prompt, Location = new Point(10, 10), Width = 285 };
        var box  = new TextBox { Location = new Point(10, 35), Width = 285, Text = def };
        var ok   = new Button  { Text = "OK",     Location = new Point(120, 65), Width = 75, DialogResult = DialogResult.OK };
        var cncl = new Button  { Text = "Отмена", Location = new Point(200, 65), Width = 75, DialogResult = DialogResult.Cancel };
        dlg.AcceptButton = ok;
        dlg.CancelButton = cncl;
        dlg.Controls.AddRange(new Control[] { lbl, box, ok, cncl });
        return dlg.ShowDialog(this) == DialogResult.OK ? box.Text.Trim() : null;
    }

    private void Err(string msg) =>
        MessageBox.Show(this, msg, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

    // ── Auto-split full path pasted into folder box ───────────────────

    private void FolderBox_Leave(object? sender, EventArgs e)
    {
        string text = _folderBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // If user pasted a full file path (has extension or is an existing file), split it
        string ext = Path.GetExtension(text);
        if (!string.IsNullOrEmpty(ext) || File.Exists(text))
        {
            _folderBox.Text = Path.GetDirectoryName(text) ?? text;
            if (string.IsNullOrEmpty(_nameBox.Text.Trim()))
                _nameBox.Text = Path.GetFileNameWithoutExtension(text);
        }
    }

    // ── Event handlers ────────────────────────────────────────────────

    private async void BrowseFolder_Click(object? sender, EventArgs e)
    {
        string? path = await PickFolderAsync();
        if (path != null) _folderBox.Text = path;
    }

    private async void BrowseKeyfile_Click(object? sender, EventArgs e)
    {
        string? path = await PickOpenAsync("Выбрать ключевой файл", "Все файлы|*.*");
        if (path != null) _keyfileBox.Text = path;
    }

    private async void CreateBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(ContainerPath)) { Err("Укажите папку и имя контейнера."); return; }
        if (string.IsNullOrEmpty(Password))      { Err("Введите пароль."); return; }

        string? sizeStr = AskInput("Размер контейнера (МБ):", "Создать контейнер", "100");
        if (sizeStr == null) return;
        if (!int.TryParse(sizeStr, out int sizeMb) || sizeMb <= 0)
        {
            Err("Введите целое число больше нуля.");
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
        catch (Exception ex) { Err(ex.Message); SetStatus("Ошибка при создании."); }
        finally { SetBusy(false); }
    }

    private async void OpenBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(ContainerPath)) { Err("Укажите папку и имя контейнера."); return; }
        if (string.IsNullOrEmpty(Password))      { Err("Введите пароль."); return; }

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
        catch (Exception ex) { Err(ex.Message); SetStatus("Ошибка при открытии."); }
        finally { SetBusy(false); }
    }

    private async void AddBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(ContainerPath)) { Err("Укажите папку и имя контейнера."); return; }
        if (string.IsNullOrEmpty(Password))      { Err("Введите пароль."); return; }

        string? filePath = await PickOpenAsync("Выбрать файл", "Все файлы|*.*");
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
        catch (Exception ex) { Err(ex.Message); SetStatus("Ошибка при добавлении."); }
        finally { SetBusy(false); }
    }

    private async void ExtractBtn_Click(object? sender, EventArgs e)
    {
        if (_session == null)            { Err("Сначала откройте контейнер."); return; }
        if (_fileList.SelectedIndex < 0) { Err("Выберите файл из списка."); return; }

        string fileName = _session.Files[_fileList.SelectedIndex].Name;

        string? destPath = await PickSaveAsync("Сохранить как…", "Все файлы|*.*", fileName);
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
        catch (Exception ex) { Err(ex.Message); SetStatus("Ошибка при извлечении."); }
        finally { SetBusy(false); }
    }

    private async void DeleteBtn_Click(object? sender, EventArgs e)
    {
        if (_session == null)            { Err("Сначала откройте контейнер."); return; }
        if (_fileList.SelectedIndex < 0) { Err("Выберите файл из списка."); return; }

        string fileName = _session.Files[_fileList.SelectedIndex].Name;

        if (MessageBox.Show(this, $"Удалить «{fileName}» из контейнера?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

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
        catch (Exception ex) { Err(ex.Message); SetStatus("Ошибка при удалении."); }
        finally { SetBusy(false); }
    }

    private async void IntegrityBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(ContainerPath)) { Err("Укажите папку и имя контейнера."); return; }
        if (string.IsNullOrEmpty(Password))      { Err("Введите пароль."); return; }

        SetBusy(true);
        SetStatus("Проверка целостности…");
        try
        {
            string path = ContainerPath, pass = Password;
            string? kf  = KeyfilePath;
            await Task.Run(() => _manager.Open(path, pass, kf));
            SetStatus("Целостность подтверждена — HMAC совпадает.");
            MessageBox.Show(this, "HMAC совпадает. Контейнер не повреждён.", "Проверка пройдена",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            SetStatus("Проверка не пройдена: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Целостность нарушена", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { SetBusy(false); }
    }
}
