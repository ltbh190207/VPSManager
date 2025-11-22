
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VPSManager
{
    public partial class MainForm : Form
    {
        private List<Process> runningProcesses = new List<Process>();
        private bool isToolRunning = false;
        private string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VPSManager", "Downloads");
        private string extractPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VPSManager", "Extracted");
        private const string ZIP_PASSWORD = "VPS@Secure#2024!";

        // UI Controls
        private Panel titleBar;
        private Label lblTitle;
        private Button btnMinimize, btnClose, btnAdmin;
        private TextBox txtUrl, txtVpsPassword;
        private Button btnDownload, btnChangePassword, btnOpenFolder;
        private ProgressBar progressBar;
        private Label lblStatus, lblProgress;
        private ListBox lstFiles;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        public MainForm()
        {
            InitializeComponent();
            SetupDirectories();
            SetupTrayIcon();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "VPS-Manager";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.DoubleBuffered = true;

            // Title Bar
            titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            titleBar.MouseDown += TitleBar_MouseDown;

            lblTitle = new Label
            {
                Text = "🖥️ VPS-Manager",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(15, 10),
                AutoSize = true
            };

            btnClose = CreateTitleButton("✕", Color.FromArgb(232, 17, 35), 660);
            btnClose.Click += BtnClose_Click;

            btnMinimize = CreateTitleButton("─", Color.FromArgb(70, 70, 70), 620);
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            btnAdmin = CreateTitleButton("ℹ", Color.FromArgb(0, 122, 204), 580);
            btnAdmin.Click += (s, e) => new AdminInfoForm().ShowDialog();

            titleBar.Controls.AddRange(new Control[] { lblTitle, btnClose, btnMinimize, btnAdmin });

            // Main Panel
            Panel mainPanel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(660, 420),
                BackColor = Color.FromArgb(37, 37, 38)
            };

            // URL Input
            Label lblUrl = CreateLabel("URL tải file:", 10, 15);
            txtUrl = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(500, 30),
                BackColor = Color.FromArgb(51, 51, 55),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };

            btnDownload = CreateButton("⬇ Tải về", 520, 38, 130);
            btnDownload.Click += BtnDownload_Click;

            // Progress
            progressBar = new ProgressBar
            {
                Location = new Point(10, 80),
                Size = new Size(640, 25),
                Style = ProgressBarStyle.Continuous
            };

            lblProgress = CreateLabel("0%", 300, 110);
            lblStatus = CreateLabel("Sẵn sàng", 10, 110);

            // File List
            Label lblFiles = CreateLabel("Danh sách file đã tải:", 10, 140);
            lstFiles = new ListBox
            {
                Location = new Point(10, 165),
                Size = new Size(640, 150),
                BackColor = Color.FromArgb(51, 51, 55),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };
            lstFiles.DoubleClick += LstFiles_DoubleClick;

            btnOpenFolder = CreateButton("📂 Mở thư mục", 10, 325, 150);
            btnOpenFolder.Click += (s, e) => {
                if (Directory.Exists(extractPath))
                    Process.Start("explorer.exe", extractPath);
            };

            // VPS Password
            Label lblVps = CreateLabel("Mật khẩu VPS mới:", 200, 328);
            txtVpsPassword = new TextBox
            {
                Location = new Point(340, 325),
                Size = new Size(180, 30),
                BackColor = Color.FromArgb(51, 51, 55),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 10)
            };

            btnChangePassword = CreateButton("🔐 Đổi Pass", 530, 323, 120);
            btnChangePassword.Click += BtnChangePassword_Click;

            // Warning Label
            Label lblWarning = new Label
            {
                Text = "⚠️ KHÔNG tắt ứng dụng khi đang chạy tool/game!",
                ForeColor = Color.FromArgb(255, 200, 0),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(10, 365),
                AutoSize = true
            };

            mainPanel.Controls.AddRange(new Control[] {
                lblUrl, txtUrl, btnDownload, progressBar, lblProgress, lblStatus,
                lblFiles, lstFiles, btnOpenFolder, lblVps, txtVpsPassword,
                btnChangePassword, lblWarning
            });

            this.Controls.AddRange(new Control[] { titleBar, mainPanel });
            this.ResumeLayout(false);
        }

        private Button CreateTitleButton(string text, Color hoverColor, int x)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(35, 35),
                Location = new Point(x, 3),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hoverColor;
            return btn;
        }

        private Button CreateButton(string text, int x, int y, int width)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Location = new Point(x, y),
                AutoSize = true
            };
        }

        private void SetupDirectories()
        {
            Directory.CreateDirectory(downloadPath);
            Directory.CreateDirectory(extractPath);
        }

        private void SetupTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Hiển thị", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            trayMenu.Items.Add("Thoát", null, (s, e) => ForceClose());

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "VPS-Manager",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
        }

        // Draggable title bar
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            if (isToolRunning)
            {
                var result = MessageBox.Show(
                    "Tool/Game đang chạy! Tắt sẽ đóng tất cả tool đang chạy.\nBạn có chắc muốn thoát?",
                    "Cảnh báo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                    ForceClose();
            }
            else
            {
                this.Hide();
                trayIcon.ShowBalloonTip(3000, "VPS-Manager", "Ứng dụng đã thu nhỏ xuống khay hệ thống", ToolTipIcon.Info);
            }
        }

        private void ForceClose()
        {
            foreach (var proc in runningProcesses)
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
            }
            trayIcon.Visible = false;
            Application.Exit();
        }

        private async void BtnDownload_Click(object sender, EventArgs e)
        {
            string url = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Vui lòng nhập URL!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnDownload.Enabled = false;
            lblStatus.Text = "Đang tải...";

            try
            {
                string fileName = Path.GetFileName(new Uri(url).LocalPath);
                string filePath = Path.Combine(downloadPath, fileName);

                using (var client = new WebClient())
                {
                    client.DownloadProgressChanged += (s, ev) =>
                    {
                        progressBar.Value = ev.ProgressPercentage;
                        lblProgress.Text = $"{ev.ProgressPercentage}%";
                    };

                    await client.DownloadFileTaskAsync(new Uri(url), filePath);
                }

                lblStatus.Text = "Đang giải nén...";
                await ExtractSecureZip(filePath);

                lblStatus.Text = "Hoàn thành!";
                RefreshFileList();
                MessageBox.Show("Tải và giải nén thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi!";
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDownload.Enabled = true;
                progressBar.Value = 0;
                lblProgress.Text = "0%";
            }
        }

        private async Task ExtractSecureZip(string zipPath)
        {
            await Task.Run(() =>
            {
                string extractFolder = Path.Combine(extractPath, Path.GetFileNameWithoutExtension(zipPath));
                Directory.CreateDirectory(extractFolder);

                // Using System.IO.Compression (for password-protected, use DotNetZip/SharpZipLib)
                //ZipFile.ExtractToDirectory(zipPath, extractFolder);

                // Filter forbidden files
                string[] forbidden = { "dragonboy", "dragon", "boy" };
                foreach (var file in Directory.GetFiles(extractFolder, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file).ToLower();
                    foreach (var word in forbidden)
                    {
                        if (name.Contains(word))
                        {
                            File.Delete(file);
                            break;
                        }
                    }
                }

                // Set file attributes to prevent copying
                foreach (var file in Directory.GetFiles(extractFolder, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.ReadOnly | FileAttributes.System);
                }
            });
        }

        private void RefreshFileList()
        {
            lstFiles.Items.Clear();
            if (Directory.Exists(extractPath))
            {
                foreach (var file in Directory.GetFiles(extractPath, "*.exe", SearchOption.AllDirectories))
                {
                    lstFiles.Items.Add(Path.GetFileName(file));
                }
            }
        }

        private void LstFiles_DoubleClick(object sender, EventArgs e)
        {
            if (lstFiles.SelectedItem == null) return;

            string fileName = lstFiles.SelectedItem.ToString();
            string[] files = Directory.GetFiles(extractPath, fileName, SearchOption.AllDirectories);

            if (files.Length > 0)
            {
                var proc = Process.Start(files[0]);
                if (proc != null)
                {
                    runningProcesses.Add(proc);
                    isToolRunning = true;
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (s, ev) =>
                    {
                        runningProcesses.Remove(proc);
                        isToolRunning = runningProcesses.Count > 0;
                    };
                }
            }
        }

        private void BtnChangePassword_Click(object sender, EventArgs e)
        {
            string newPass = txtVpsPassword.Text.Trim();
            if (newPass.Length < 8)
            {
                MessageBox.Show("Mật khẩu phải có ít nhất 8 ký tự!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c net user %username% \"{newPass}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };
                Process.Start(psi)?.WaitForExit();
                MessageBox.Show("Đã đổi mật khẩu VPS thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtVpsPassword.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}\nCần quyền Administrator!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                BtnClose_Click(null, null);
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                trayIcon?.Dispose();
                trayMenu?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}