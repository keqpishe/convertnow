using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace ConvertNow.Installer;

public sealed class InstallerForm : Form
{
    private readonly bool _isUninstallMode;
    private TextBox _txtPath = null!;
    private Button _btnBrowse = null!;
    private CheckBox _chkContextMenu = null!;
    private CheckBox _chkDesktop = null!;
    private CheckBox _chkStartMenu = null!;
    private CheckBox _chkRestartExplorer = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblStatus = null!;
    private Button _btnAction = null!;
    private Button _btnCancel = null!;
    private Panel _pnlHeader = null!;

    public InstallerForm(bool isUninstallMode = false)
    {
        _isUninstallMode = isUninstallMode;
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = _isUninstallMode ? "Convert.Now — Uninstall" : "Convert.Now — Setup";
        Size = new Size(500, 390);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
        BackColor = Color.FromArgb(249, 250, 251);

        try
        {
            string iconCandidate = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconCandidate))
            {
                Icon = new Icon(iconCandidate);
            }
        }
        catch
        {
        }

        // Header banner
        _pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(37, 99, 235) // Modern Indigo/Blue
        };
        _pnlHeader.Paint += (s, e) =>
        {
            using var brush = new LinearGradientBrush(_pnlHeader.ClientRectangle,
                Color.FromArgb(37, 99, 235), Color.FromArgb(79, 70, 229), 45f);
            e.Graphics.FillRectangle(brush, _pnlHeader.ClientRectangle);
        };

        var lblTitle = new Label
        {
            Text = _isUninstallMode ? "Удаление Convert.Now" : "Установка Convert.Now",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(20, 14)
        };
        _pnlHeader.Controls.Add(lblTitle);

        var lblSub = new Label
        {
            Text = _isUninstallMode
                ? "Удаление программы и пункта контекстного меню"
                : "Универсальный файловый конвертер с интеграцией в Проводник",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(224, 231, 255),
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(22, 40)
        };
        _pnlHeader.Controls.Add(lblSub);
        Controls.Add(_pnlHeader);

        int top = 88;

        if (!_isUninstallMode)
        {
            var lblDir = new Label
            {
                Text = "Папка установки:",
                Location = new Point(22, top),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            Controls.Add(lblDir);
            top += 22;

            _txtPath = new TextBox
            {
                Text = InstallEngine.DefaultInstallDir,
                Location = new Point(22, top),
                Size = new Size(350, 26),
                ReadOnly = false
            };
            Controls.Add(_txtPath);

            _btnBrowse = new Button
            {
                Text = "Обзор...",
                Location = new Point(380, top - 1),
                Size = new Size(88, 28)
            };
            _btnBrowse.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog();
                fbd.SelectedPath = _txtPath.Text;
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtPath.Text = fbd.SelectedPath;
                }
            };
            Controls.Add(_btnBrowse);
            top += 38;

            _chkContextMenu = new CheckBox
            {
                Text = "Добавить кнопку \"Convert now\" в контекстное меню файлов",
                Checked = true,
                Location = new Point(22, top),
                AutoSize = true
            };
            Controls.Add(_chkContextMenu);
            top += 25;

            _chkStartMenu = new CheckBox
            {
                Text = "Создать ярлык в меню \"Пуск\"",
                Checked = true,
                Location = new Point(22, top),
                AutoSize = true
            };
            Controls.Add(_chkStartMenu);
            top += 25;

            _chkDesktop = new CheckBox
            {
                Text = "Создать ярлык на Рабочем столе",
                Checked = true,
                Location = new Point(22, top),
                AutoSize = true
            };
            Controls.Add(_chkDesktop);
            top += 25;

            _chkRestartExplorer = new CheckBox
            {
                Text = "Перезапустить Проводник (для мгновенного появления кнопки)",
                Checked = true,
                Location = new Point(22, top),
                AutoSize = true
            };
            Controls.Add(_chkRestartExplorer);
            top += 30;
        }
        else
        {
            var lblWarning = new Label
            {
                Text = "Вы уверены, что хотите удалить Convert.Now?\n" +
                       "Это действие удалит программу и пункт \"Convert now\" из контекстного меню.",
                Location = new Point(22, top),
                Size = new Size(440, 50),
                ForeColor = Color.FromArgb(55, 65, 81)
            };
            Controls.Add(lblWarning);
            top += 70;
        }

        _lblStatus = new Label
        {
            Text = _isUninstallMode ? "Готов к удалению." : "Готов к установке.",
            Location = new Point(22, top),
            Size = new Size(446, 20),
            ForeColor = Color.FromArgb(75, 85, 99)
        };
        Controls.Add(_lblStatus);
        top += 22;

        _progressBar = new ProgressBar
        {
            Location = new Point(22, top),
            Size = new Size(446, 18),
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };
        Controls.Add(_progressBar);
        top += 30;

        // Bottom action buttons
        _btnAction = new Button
        {
            Text = _isUninstallMode ? "Удалить" : "Установить",
            Location = new Point(252, top),
            Size = new Size(116, 32),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnAction.FlatAppearance.BorderSize = 0;
        _btnAction.Click += OnActionClick;
        Controls.Add(_btnAction);

        _btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(376, top),
            Size = new Size(92, 32)
        };
        _btnCancel.Click += (s, e) => Close();
        Controls.Add(_btnCancel);
        ClientSize = new Size(484, top + 50);
    }

    private async void OnActionClick(object? sender, EventArgs e)
    {
        if (_btnAction.Text is "Готово" or "Закрыть")
        {
            Close();
            return;
        }

        if (_btnAction.Text is "Запустить Convert.Now")
        {
            string targetDir = _txtPath?.Text ?? InstallEngine.DefaultInstallDir;
            string exe = Path.Combine(targetDir, "ConvertNow.exe");
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
            }
            Close();
            return;
        }

        _btnAction.Enabled = false;
        _btnCancel.Enabled = false;
        if (_btnBrowse != null) _btnBrowse.Enabled = false;
        if (_txtPath != null) _txtPath.Enabled = false;

        string target = _txtPath?.Text ?? InstallEngine.DefaultInstallDir;
        bool desktop = _chkDesktop?.Checked ?? false;
        bool startMenu = _chkStartMenu?.Checked ?? false;
        bool restartExplorer = _chkRestartExplorer?.Checked ?? false;
        bool contextMenu = _chkContextMenu?.Checked ?? false;

        try
        {
            if (!_isUninstallMode)
            {
                await Task.Run(() =>
                {
                    InstallEngine.Install(
                        target,
                        (percent, msg) => Invoke(() =>
                        {
                            _progressBar.Value = Math.Clamp(percent, 0, 100);
                            _lblStatus.Text = msg;
                        }),
                        desktop,
                        startMenu,
                        restartExplorer,
                        contextMenu);
                });

                _lblStatus.ForeColor = Color.FromArgb(16, 149, 193);
                _lblStatus.Text = contextMenu
                    ? "Установка завершена! Кнопка добавлена в Проводник."
                    : "Установка завершена.";
                _btnAction.Text = "Запустить Convert.Now";
                _btnAction.Size = new Size(160, 32);
                _btnAction.Location = new Point(208, _btnAction.Location.Y);
                _btnCancel.Text = "Закрыть";
            }
            else
            {
                await Task.Run(() =>
                {
                    InstallEngine.Uninstall((percent, msg) => Invoke(() =>
                    {
                        _progressBar.Value = Math.Clamp(percent, 0, 100);
                        _lblStatus.Text = msg;
                    }));
                });

                _lblStatus.Text = "Convert.Now успешно удалён.";
                _btnAction.Text = "Закрыть";
                _btnCancel.Visible = false;
            }
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = Color.Red;
            _lblStatus.Text = $"Ошибка: {ex.Message}";
            MessageBox.Show(this, $"Произошла ошибка:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnAction.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }
}
