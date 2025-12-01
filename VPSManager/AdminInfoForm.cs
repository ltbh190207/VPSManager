using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VPSManager
{
    public class AdminInfoForm : Form
    {
        private Panel titleBar;
        public AdminInfoForm()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // Form settings
            this.Text = "Thông tin Admin";
            this.Size = new Size(450, 350);
            this.StartPosition = FormStartPosition.CenterParent;
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
            Label lblTitle = new Label
            {
                Text = "ℹ️ Thông tin Admin",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(15, 10),
                AutoSize = true
            };
            Button btnClose = new Button
            {
                Text = "✕",
                Size = new Size(35, 35),
                Location = new Point(410, 3),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            btnClose.Click += (s, e) => this.Close();
            titleBar.Controls.AddRange(new Control[] { lblTitle, btnClose });
            // Content Panel
            Panel contentPanel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(410, 270),
                BackColor = Color.FromArgb(37, 37, 38)
            };
            // Logo/Icon
            Label lblIcon = new Label
            {
                Text = "🖥️",
                Font = new Font("Segoe UI", 35),
                ForeColor = Color.White,
                Location = new Point(171, 10),
                AutoSize = true
            };
            // App Name
            Label lblAppName = new Label
            {
                Text = "VPS-Manager",
                ForeColor = Color.FromArgb(0, 122, 204),
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Location = new Point(115, 80),
                AutoSize = true
            };
            // Version
            Label lblVersion = new Label
            {
                Text = "Phiên bản: 1.0.0",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Location = new Point(150, 115),
                AutoSize = true
            };
            // Separator
            Panel separator = new Panel
            {
                Location = new Point(20, 145),
                Size = new Size(370, 1),
                BackColor = Color.FromArgb(70, 70, 70)
            };
            // Admin Info
            Label lblAdminTitle = new Label
            {
                Text = "👤 Thông tin liên hệ",
                ForeColor = Color.FromArgb(255, 200, 0),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(20, 160),
                AutoSize = true
            };
            Label lblAdmin = new Label
            {
                Text = "Admin: VPS-Admin",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 190),
                AutoSize = true
            };
            Label lblContact = new Label
            {
                Text = "📧 Email: admin@vpsmanager.com",
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 215),
                AutoSize = true
            };
            Label lblTelegram = new Label
            {
                Text = "📱 Telegram: @vpsmanager",
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 240),
                AutoSize = true
            };
            // Copyright
            Label lblCopyright = new Label
            {
                Text = "© 2024 VPS-Manager. All rights reserved.",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8),
                Location = new Point(105, 320),
                AutoSize = true
            };
            contentPanel.Controls.AddRange(new Control[] {
                lblIcon, lblAppName, lblVersion, separator,
                lblAdminTitle, lblAdmin, lblContact, lblTelegram
            });
            this.Controls.AddRange(new Control[] { titleBar, contentPanel, lblCopyright });
            this.ResumeLayout(false);
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
    }
}