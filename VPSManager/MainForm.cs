// MainForm.cs - Phần UI và logic chính, gọi đến LicenseManager để xác thực
using CG.Web.MegaApiClient;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VPSManager
{
    public class DownloadItem
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Password { get; set; }
        public string ExtractFolder { get; set; }
        public DownloadItem(string name, string url, string password = "")
        {
            Name = name;
            Url = url;
            Password = password;
        }
    }

    public partial class MainForm : Form
    {
        #region Download Links
        private readonly List<DownloadItem> downloadItems = new List<DownloadItem>
        {
            new DownloadItem("Tool Auto Game 1", "https://mega.nz/folder/NXRTWRaA#46cj7MkB7VfP1J7TdRsuGg/file/sTwBHYYK", ""),
            new DownloadItem("Tool Auto Game 2", "https://mega.nz/file/XXXXXXXX#YYYYYYYY", "secure@2024"),
            new DownloadItem("Tien ich He thong", "https://example.com/utility.zip", ""),
            new DownloadItem("Pack Resource", "https://example.com/resource.zip", "res#pack")
        };
        #endregion

        #region Fields
        private List<Process> runningProcesses = new List<Process>();
        private bool isToolRunning = false;
        private bool isDownloading = false;
        private string appDataPath, downloadPath, extractPath;
        private string[] forbiddenWords = { "dragonboy", "dragon", "boy" };
        private MegaApiClient megaClient;
        private Panel titleBar, statusPanel;
        private Label lblTitle, lblStatus, lblCpu, lblRam, lblProgress;
        private Button btnMinimize, btnClose, btnAdmin, btnDownload, btnDownloadAll, btnChangePassword, btnRun, btnDelete;
        private ListView lstDownloads;
        private TextBox txtVpsPassword;
        private ProgressBar progressBar;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private Timer perfTimer;
        private PerformanceCounter cpuCounter, ramCounter;
        private HttpClient httpClient;
        private LicenseManager licenseManager;
        #endregion

        public MainForm()
        {
            httpClient = new HttpClient();
            SetupPaths();
            string currentHWID = GetHardwareID();
            licenseManager = new LicenseManager(appDataPath, httpClient, currentHWID);
            if (!licenseManager.CheckLicense())
            {
                Application.Exit();
                return;
            }
            InitializeComponent();
            SetupTrayIcon();
            SetupPerformanceMonitor();
            SetupMegaClient();
            LoadDownloadList();
            MessageBox.Show("MainForm constructor chạy", "Debug");

        }

        private string GetHardwareID()
        {
            try
            {
                string hwid = "";
                hwid += Environment.MachineName;
                hwid += Environment.UserName;
                hwid += Environment.ProcessorCount.ToString();
                try
                {
                    var drive = new DriveInfo("C");
                    hwid += drive.VolumeLabel;
                }
                catch { }
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hwid));
                    StringBuilder builder = new StringBuilder();
                    foreach (byte b in bytes)
                        builder.Append(b.ToString("x2"));
                    return builder.ToString().Substring(0, 32);
                }
            }
            catch
            {
                return "UNKNOWN-HWID";
            }
        }

        private void SetupPaths()
        {
            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VPSManager");
            downloadPath = Path.Combine(appDataPath, "Downloads");
            extractPath = Path.Combine(appDataPath, "Tools");
            Directory.CreateDirectory(downloadPath);
            Directory.CreateDirectory(extractPath);
        }

        private void SetupMegaClient()
        {
            megaClient = new MegaApiClient();
            megaClient.LoginAnonymous();
        }

        #region UI Initialization
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "VPS-Manager";
            this.Size = new Size(650, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.DoubleBuffered = true;
            titleBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(45, 45, 48) };
            titleBar.MouseDown += TitleBar_MouseDown;
            lblTitle = new Label
            {
                Text = "VPS-Manager v1.0 [Licensed]",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(12, 10),
                AutoSize = true
            };
            btnClose = CreateTitleBtn("X", 610, Color.FromArgb(232, 17, 35));
            btnClose.Click += BtnClose_Click;
            btnMinimize = CreateTitleBtn("-", 575, Color.FromArgb(60, 60, 60));
            btnMinimize.Click += BtnMinimize_Click;
            btnAdmin = CreateTitleBtn("i", 540, Color.FromArgb(0, 122, 204));
            btnAdmin.Click += BtnAdmin_Click;
            titleBar.Controls.AddRange(new Control[] { lblTitle, btnClose, btnMinimize, btnAdmin });
            Label lblList = new Label
            {
                Text = "Danh sach Tool/Game:",
                ForeColor = Color.FromArgb(0, 180, 220),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(15, 50),
                AutoSize = true
            };
            lstDownloads = new ListView
            {
                Location = new Point(15, 75),
                Size = new Size(620, 180),
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                GridLines = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            lstDownloads.Columns.Add("Ten", 320);
            lstDownloads.Columns.Add("Trang thai", 150);
            lstDownloads.Columns.Add("Loai", 80);
            btnDownload = CreateBtn("Tai muc chon", 15, 265, 140, Color.FromArgb(0, 122, 204));
            btnDownload.Click += BtnDownload_Click;
            btnDownloadAll = CreateBtn("Tai tat ca", 165, 265, 120, Color.FromArgb(0, 150, 80));
            btnDownloadAll.Click += BtnDownloadAll_Click;
            btnRun = CreateBtn("Chay muc chon", 300, 265, 140, Color.FromArgb(0, 180, 80));
            btnRun.Click += BtnRun_Click;
            btnDelete = CreateBtn("Xoa muc chon", 450, 265, 140, Color.FromArgb(232, 17, 35));
            btnDelete.Click += BtnDelete_Click;
            progressBar = new ProgressBar
            {
                Location = new Point(15, 300),
                Size = new Size(580, 25),
                Style = ProgressBarStyle.Continuous
            };
            lblProgress = new Label
            {
                Text = "0%",
                ForeColor = Color.Lime,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(600, 303),
                AutoSize = true
            };
            Label lblVps = new Label
            {
                Text = "Doi mat khau VPS:",
                ForeColor = Color.FromArgb(255, 180, 0),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(15, 340),
                AutoSize = true
            };
            Label lblPassLabel = new Label
            {
                Text = "Mat khau moi:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(15, 370),
                AutoSize = true
            };
            txtVpsPassword = new TextBox
            {
                Location = new Point(120, 367),
                Size = new Size(280, 25),
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                UseSystemPasswordChar = true
            };
            btnChangePassword = CreateBtn("Doi mat khau", 420, 365, 130, Color.FromArgb(200, 80, 0));
            btnChangePassword.Click += BtnChangePassword_Click;
            Label lblWarning = new Label
            {
                Text = "! KHONG tat ung dung khi dang chay tool/game!",
                ForeColor = Color.FromArgb(255, 100, 100),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(15, 410),
                AutoSize = true
            };
            statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Color.FromArgb(0, 122, 204) };
            lblStatus = new Label
            {
                Text = "San sang",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(10, 5),
                AutoSize = true
            };
            lblCpu = new Label
            {
                Text = "CPU: 0%",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(450, 5),
                AutoSize = true
            };
            lblRam = new Label
            {
                Text = "RAM: 0 MB",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(540, 5),
                AutoSize = true
            };
            statusPanel.Controls.AddRange(new Control[] { lblStatus, lblCpu, lblRam });
            this.Controls.AddRange(new Control[] {
                titleBar, lblList, lstDownloads, btnDownload, btnDownloadAll, btnRun, btnDelete,
                progressBar, lblProgress, lblVps, lblPassLabel, txtVpsPassword,
                btnChangePassword, lblWarning, statusPanel
            });
            this.ResumeLayout(false);
        }

        private Button CreateTitleBtn(string text, int x, Color hover)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(30, 30),
                Location = new Point(x, 5),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hover;
            return btn;
        }

        private Button CreateBtn(string text, int x, int y, int w, Color bg)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void BtnAdmin_Click(object sender, EventArgs e)
        {
            licenseManager.ShowLicenseInfo();
        }
        #endregion

        #region Download List
        private string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private string GetExtractFolder(DownloadItem item)
        {
            return Path.Combine(extractPath, SanitizeName(item.Name));
        }

        private void LoadDownloadList()
        {
            lstDownloads.Items.Clear();
            foreach (var item in downloadItems)
            {
                var lvi = new ListViewItem(item.Name);
                string folder = GetExtractFolder(item);
                if (Directory.Exists(folder))
                {
                    lvi.SubItems.Add("Da tai");
                    item.ExtractFolder = folder;
                }
                else
                {
                    lvi.SubItems.Add("Chua tai");
                }
                lvi.SubItems.Add(IsMegaLink(item.Url) ? "MEGA" : "Direct");
                lvi.Tag = item;
                lstDownloads.Items.Add(lvi);
            }
        }

        private bool IsMegaLink(string url)
        {
            return url.Contains("mega.nz") || url.Contains("mega.co.nz");
        }

        private void UpdateStatus(string text, Color color)
        {
            if (InvokeRequired) { Invoke((Action)(() => UpdateStatus(text, color))); return; }
            lblStatus.Text = text;
            statusPanel.BackColor = color;
        }

        private void UpdateListStatus(int idx, string status)
        {
            if (InvokeRequired) { Invoke((Action)(() => UpdateListStatus(idx, status))); return; }
            if (idx >= 0 && idx < lstDownloads.Items.Count)
                lstDownloads.Items[idx].SubItems[1].Text = status;
        }

        private void UpdateProgress(int val)
        {
            if (InvokeRequired) { Invoke((Action)(() => UpdateProgress(val))); return; }
            progressBar.Value = Math.Min(100, Math.Max(0, val));
            lblProgress.Text = val + "%";
        }
        #endregion

        #region Download & Extract
        private async void BtnDownload_Click(object sender, EventArgs e)
        {
            var selected = new List<int>();
            for (int i = 0; i < lstDownloads.Items.Count; i++)
                if (lstDownloads.Items[i].Checked) selected.Add(i);
            if (selected.Count == 0)
            {
                MessageBox.Show("Chon it nhat 1 muc!", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            await DownloadItems(selected);
        }

        private async void BtnDownloadAll_Click(object sender, EventArgs e)
        {
            var all = new List<int>();
            for (int i = 0; i < lstDownloads.Items.Count; i++) all.Add(i);
            await DownloadItems(all);
        }

        private async Task DownloadItems(List<int> indices)
        {
            if (isDownloading)
            { MessageBox.Show("Dang tai, vui long cho!", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            isDownloading = true;
            btnDownload.Enabled = false;
            btnDownloadAll.Enabled = false;
            btnRun.Enabled = false;
            btnDelete.Enabled = false;
            try
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    int idx = indices[i];
                    var item = lstDownloads.Items[idx].Tag as DownloadItem;
                    if (item == null) continue;
                    UpdateStatus("Dang tai: " + item.Name, Color.FromArgb(255, 150, 0));
                    UpdateListStatus(idx, "Dang tai...");
                    try
                    {
                        string filePath;
                        if (IsMegaLink(item.Url))
                        {
                            filePath = await DownloadFromMega(item.Url, item.Name);
                        }
                        else
                        {
                            filePath = await DownloadDirect(item.Url, item.Name);
                        }
                        UpdateListStatus(idx, "Giai nen...");
                        UpdateStatus("Giai nen: " + item.Name, Color.FromArgb(0, 150, 200));
                        string folder = await ExtractZip(filePath, item.Password, item);
                        UpdateListStatus(idx, "Da tai");
                        await RunExe(folder);
                        try { File.Delete(filePath); } catch { }
                    }
                    catch (Exception ex)
                    {
                        UpdateListStatus(idx, "Loi!");
                        MessageBox.Show("Loi tai " + item.Name + ": " + ex.Message, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                UpdateStatus("Hoan thanh!", Color.FromArgb(0, 180, 80));
                UpdateProgress(100);
                trayIcon.ShowBalloonTip(2000, "VPS-Manager", "Da tai va chay xong!", ToolTipIcon.Info);
            }
            finally
            {
                isDownloading = false;
                btnDownload.Enabled = true;
                btnDownloadAll.Enabled = true;
                btnRun.Enabled = true;
                btnDelete.Enabled = true;
            }
        }

        private async Task<string> DownloadFromMega(string url, string name)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!megaClient.IsLoggedIn)
                        megaClient.LoginAnonymous();
                    Uri uri = new Uri(url);
                    INode node = null;
                    if (url.Contains("/folder/") && url.Contains("/file/"))
                    {
                        string folderUrl = url.Substring(0, url.IndexOf("/file/"));
                        string fileId = url.Substring(url.LastIndexOf("/file/") + 6);
                        var nodes = megaClient.GetNodesFromLink(new Uri(folderUrl));
                        foreach (var n in nodes)
                        {
                            if (n.Id == fileId || n.Type == NodeType.File)
                            {
                                if (n.Id == fileId)
                                {
                                    node = n;
                                    break;
                                }
                            }
                        }
                        if (node == null)
                        {
                            foreach (var n in nodes)
                            {
                                if (n.Type == NodeType.File && n.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                {
                                    node = n;
                                    break;
                                }
                            }
                        }
                    }
                    else if (url.Contains("/file/"))
                    {
                        node = megaClient.GetNodeFromLink(uri);
                    }
                    else if (url.Contains("/folder/"))
                    {
                        var nodes = megaClient.GetNodesFromLink(uri);
                        foreach (var n in nodes)
                        {
                            if (n.Type == NodeType.File && n.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                node = n;
                                break;
                            }
                        }
                    }
                    if (node == null)
                        throw new Exception("Khong tim thay file trong link MEGA!");
                    string fileName = node.Name;
                    string filePath = Path.Combine(downloadPath, fileName);
                    using (var stream = megaClient.Download(node))
                    using (var fileStream = File.Create(filePath))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        long totalRead = 0;
                        long totalSize = node.Size;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fileStream.Write(buffer, 0, read);
                            totalRead += read;
                            int percent = (int)((totalRead * 100) / totalSize);
                            UpdateProgress(percent);
                        }
                    }
                    return filePath;
                }
                catch (Exception ex)
                {
                    throw new Exception("Loi tai tu MEGA: " + ex.Message);
                }
            });
        }

        private async Task<string> DownloadDirect(string url, string name)
        {
            string safeName = SanitizeName(name);
            string filePath = Path.Combine(downloadPath, safeName + ".zip");
            using (var client = new System.Net.WebClient())
            {
                client.Headers.Add("User-Agent", "VPS-Manager/1.0");
                client.DownloadProgressChanged += (s, e) => UpdateProgress(e.ProgressPercentage);
                await client.DownloadFileTaskAsync(new Uri(url), filePath);
            }
            return filePath;
        }

        private async Task<string> ExtractZip(string zipPath, string password, DownloadItem item)
        {
            return await Task.Run(() =>
            {
                string folder = GetExtractFolder(item);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Directory.CreateDirectory(folder);
                using (var fs = File.OpenRead(zipPath))
                using (var zip = new ZipFile(fs))
                {
                    if (!string.IsNullOrEmpty(password))
                        zip.Password = password;
                    foreach (ZipEntry entry in zip)
                    {
                        if (!entry.IsFile) continue;
                        string name = entry.Name.ToLower();
                        bool skip = false;
                        foreach (var word in forbiddenWords)
                            if (name.Contains(word)) { skip = true; break; }
                        if (skip) continue;
                        string path = Path.Combine(folder, entry.Name);
                        string dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        using (var input = zip.GetInputStream(entry))
                        using (var output = File.Create(path))
                            input.CopyTo(output);
                        try { File.SetAttributes(path, FileAttributes.ReadOnly); } catch { }
                    }
                }
                item.ExtractFolder = folder;
                return folder;
            });
        }

        private async Task RunExe(string folder)
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(folder)) return;
                foreach (var exe in Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(exe).ToLower();
                    bool skip = false;
                    foreach (var word in forbiddenWords)
                        if (name.Contains(word)) { skip = true; break; }
                    if (skip) continue;
                    try
                    {
                        Invoke((Action)(() =>
                        {
                            var proc = Process.Start(new ProcessStartInfo
                            {
                                FileName = exe,
                                WorkingDirectory = Path.GetDirectoryName(exe),
                                UseShellExecute = true
                            });
                            if (proc != null)
                            {
                                runningProcesses.Add(proc);
                                isToolRunning = true;
                                UpdateStatus("Dang chay: " + Path.GetFileName(exe), Color.FromArgb(0, 180, 80));
                                proc.EnableRaisingEvents = true;
                                proc.Exited += (s, e) =>
                                {
                                    Invoke((Action)(() =>
                                    {
                                        runningProcesses.Remove(proc);
                                        isToolRunning = runningProcesses.Count > 0;
                                        if (!isToolRunning) UpdateStatus("San sang", Color.FromArgb(0, 122, 204));
                                    }));
                                };
                            }
                        }));
                        break;
                    }
                    catch { }
                }
            });
        }
        #endregion

        #region Run & Delete
        private async void BtnRun_Click(object sender, EventArgs e)
        {
            if (isDownloading || isToolRunning)
            {
                MessageBox.Show("Dang tai hoac chay, vui long cho!", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var selected = new List<int>();
            for (int i = 0; i < lstDownloads.Items.Count; i++)
                if (lstDownloads.Items[i].Checked) selected.Add(i);
            if (selected.Count == 0)
            {
                MessageBox.Show("Chon it nhat 1 muc!", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            foreach (int idx in selected)
            {
                var item = lstDownloads.Items[idx].Tag as DownloadItem;
                if (item == null || string.IsNullOrEmpty(item.ExtractFolder) || !Directory.Exists(item.ExtractFolder))
                {
                    MessageBox.Show("Muc " + item.Name + " chua tai!", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }
                UpdateStatus("Dang chay: " + item.Name, Color.FromArgb(0, 180, 80));
                await RunExe(item.ExtractFolder);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (isDownloading || isToolRunning)
            {
                MessageBox.Show("Dang tai hoac chay, vui long cho!", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var selected = new List<int>();
            for (int i = 0; i < lstDownloads.Items.Count; i++)
                if (lstDownloads.Items[i].Checked) selected.Add(i);
            if (selected.Count == 0)
            {
                MessageBox.Show("Chon it nhat 1 muc!", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show("Ban co chac xoa cac muc da chon?", "Xac nhan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            foreach (int idx in selected)
            {
                var item = lstDownloads.Items[idx].Tag as DownloadItem;
                if (item == null || string.IsNullOrEmpty(item.ExtractFolder) || !Directory.Exists(item.ExtractFolder)) continue;
                try
                {
                    RemoveReadOnlyAttributes(item.ExtractFolder);
                    Directory.Delete(item.ExtractFolder, true);
                    item.ExtractFolder = null;
                    UpdateListStatus(idx, "Chua tai");
                    UpdateStatus("Da xoa: " + item.Name, Color.FromArgb(232, 17, 35));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Loi xoa " + item.Name + ": " + ex.Message, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RemoveReadOnlyAttributes(string folder)
        {
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            foreach (var dir in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(dir, FileAttributes.Normal); } catch { }
            }
            try { File.SetAttributes(folder, FileAttributes.Normal); } catch { }
        }
        #endregion

        #region VPS Password
        private void BtnChangePassword_Click(object sender, EventArgs e)
        {
            string pass = txtVpsPassword.Text;
            if (pass.Length < 8)
            {
                MessageBox.Show("Mat khau phai co it nhat 8 ky tu!", "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c net user %username% \"" + pass + "\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                if (proc?.ExitCode == 0)
                {
                    MessageBox.Show("Doi mat khau thanh cong!", "Thanh cong", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtVpsPassword.Clear();
                }
                else MessageBox.Show("Khong the doi mat khau!", "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex) { MessageBox.Show("Loi: " + ex.Message, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        #endregion

        #region Tray & Close
        private void SetupTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Hien thi", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.Activate(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Thoat", null, (s, e) => ForceClose());
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "VPS-Manager",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.Activate(); };
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            if (runningProcesses.Count > 0)
            {
                if (MessageBox.Show("Co tool dang chay, tat se dong tat ca!\nBan co chac muon thoat?", "Canh bao", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            ForceClose();
        }

        private void BtnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
            trayIcon.ShowBalloonTip(1000, "VPS-Manager", "Ung dung da duoc thu nho vao tray.", ToolTipIcon.Info);
        }

        private void ForceClose()
        {
            foreach (var p in runningProcesses.ToArray())
                try { if (!p.HasExited) p.Kill(); } catch { }
            runningProcesses.Clear();
            try { if (megaClient != null && megaClient.IsLoggedIn) megaClient.Logout(); } catch { }
            perfTimer?.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                BtnMinimize_Click(null, null); // Minimize vao tray thay vi close
            }
            base.OnFormClosing(e);
        }
        #endregion

        #region Performance
        private void SetupPerformanceMonitor()
        {
            try
            {
                string name = Process.GetCurrentProcess().ProcessName;
                cpuCounter = new PerformanceCounter("Process", "% Processor Time", name, true);
                ramCounter = new PerformanceCounter("Process", "Working Set - Private", name, true);
                perfTimer = new Timer { Interval = 2000 };
                perfTimer.Tick += (s, e) =>
                {
                    try
                    {
                        lblCpu.Text = "CPU: " + (cpuCounter.NextValue() / Environment.ProcessorCount).ToString("0.0") + "%";
                        lblRam.Text = "RAM: " + (ramCounter.NextValue() / 1024 / 1024).ToString("0.0") + " MB";
                    }
                    catch { }
                };
                perfTimer.Start();
            }
            catch { }
        }
        #endregion

        #region Drag
        [DllImport("user32.dll")] private static extern int SendMessage(IntPtr h, int m, int w, int l);
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); }
        }
        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                perfTimer?.Dispose();
                cpuCounter?.Dispose();
                ramCounter?.Dispose();
                trayIcon?.Dispose();
                trayMenu?.Dispose();
                httpClient?.Dispose();
                try { megaClient?.Logout(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}