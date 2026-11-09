using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Management;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TempGuard
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyIcon(IntPtr hIcon);
    }

    // ---------------------------------------------------------------------
    // Config: plain INI stored under %LOCALAPPDATA%\TempGuard
    // ---------------------------------------------------------------------
    internal sealed class Config
    {
        public int IntervalSeconds = 3;
        public int ThresholdC = 80;
        public bool ShowOverlay = false;
        public bool LogEnabled = true;

        public static string DataDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TempGuard");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string FilePath { get { return Path.Combine(DataDir, "settings.ini"); } }
        public static string LogDir { get { return Path.Combine(DataDir, "logs"); } }

        public static Config Load()
        {
            var c = new Config();
            try
            {
                if (!File.Exists(FilePath)) return c;
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int p = line.IndexOf('=');
                    if (p <= 0) continue;
                    string k = line.Substring(0, p).Trim().ToLowerInvariant();
                    string v = line.Substring(p + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "interval":
                            if (int.TryParse(v, out n) && n >= 1 && n <= 300) c.IntervalSeconds = n;
                            break;
                        case "threshold":
                            if (int.TryParse(v, out n) && n >= 40 && n <= 110) c.ThresholdC = n;
                            break;
                        case "overlay":
                            c.ShowOverlay = (v == "1" || v.ToLowerInvariant() == "true");
                            break;
                        case "log":
                            c.LogEnabled = (v != "0" && v.ToLowerInvariant() != "false");
                            break;
                    }
                }
            }
            catch { }
            return c;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# TempGuard settings / 修改后重启程序生效");
                sb.AppendLine("# interval  = 采样间隔（秒，1-300）");
                sb.AppendLine("# threshold = 高温告警阈值（摄氏度，40-110）");
                sb.AppendLine("# overlay   = 是否显示右上角悬浮温度 (1/0)");
                sb.AppendLine("# log       = 是否写 CSV 日志 (1/0)");
                sb.AppendLine("interval=" + IntervalSeconds);
                sb.AppendLine("threshold=" + ThresholdC);
                sb.AppendLine("overlay=" + (ShowOverlay ? "1" : "0"));
                sb.AppendLine("log=" + (LogEnabled ? "1" : "0"));
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // ---------------------------------------------------------------------
    // TemperatureReader: ACPI thermal zones, fallback to perf counters
    // ---------------------------------------------------------------------
    internal static class TemperatureReader
    {
        public static double[] Read()
        {
            var values = new List<double>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    @"root\wmi", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject mo in collection)
                    {
                        using (mo)
                        {
                            object raw = mo["CurrentTemperature"];
                            if (raw == null) continue;
                            double tenthKelvin = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                            double celsius = Math.Round((tenthKelvin - 2732.0) / 10.0, 1);
                            if (celsius > -50 && celsius < 150) values.Add(celsius);
                        }
                    }
                }
            }
            catch { }

            if (values.Count == 0)
            {
                try
                {
                    var category = new PerformanceCounterCategory("Thermal Zone Information");
                    foreach (string instance in category.GetInstanceNames())
                    {
                        using (var counter = new PerformanceCounter(
                            "Thermal Zone Information", "Temperature", instance, true))
                        {
                            double celsius = Math.Round(counter.NextValue() - 273.15, 1);
                            if (celsius > -50 && celsius < 150) values.Add(celsius);
                        }
                    }
                }
                catch { }
            }
            return values.ToArray();
        }
    }

    // ---------------------------------------------------------------------
    // IconFactory: multi-size (16/32/48) BMP/DIB-based ICO.
    // .NET Framework 4.8's Icon(Stream) does NOT reliably accept
    // PNG-compressed ICO entries, so we emit classic 32bpp DIB images.
    // A plain GDI Icon.FromHandle fallback is kept for extra safety.
    // ---------------------------------------------------------------------
    internal static class IconFactory
    {
        private static readonly int[] Sizes = { 16, 32, 48 };

        public static Color ColorFor(int tempC, int threshold, bool available)
        {
            if (!available) return Color.FromArgb(90, 90, 96);
            if (tempC >= threshold) return Color.FromArgb(198, 40, 40);
            if (tempC >= threshold - 10) return Color.FromArgb(230, 81, 0);
            if (tempC >= threshold - 20) return Color.FromArgb(202, 138, 4);
            return Color.FromArgb(27, 118, 62);
        }

        public static Icon Create(int tempC, int threshold, bool available)
        {
            Color background = ColorFor(tempC, threshold, available);
            string text = available
                ? (tempC >= 100 ? "99" : tempC.ToString(CultureInfo.InvariantCulture))
                : "--";
            try
            {
                byte[] bytes = BuildDibIco(text, background);
                var stream = new MemoryStream(bytes);
                return new Icon(stream);
            }
            catch
            {
                // Conservative fallback: single 32x32 GDI icon.
                using (var bmp = new Bitmap(32, 32))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(background);
                    IntPtr handle = bmp.GetHicon();
                    try
                    {
                        using (var tmp = Icon.FromHandle(handle)) return (Icon)tmp.Clone();
                    }
                    finally
                    {
                        NativeMethods.DestroyIcon(handle);
                    }
                }
            }
        }

        private static byte[] BuildDibIco(string text, Color background)
        {
            var images = new List<byte[]>();
            foreach (int size in Sizes)
            {
                using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(background);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    float fontSize = text.Length >= 2 ? size * 0.54f : size * 0.66f;
                    using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var white = new SolidBrush(Color.White))
                    using (var format = new StringFormat())
                    {
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Center;
                        // Slight downward nudge keeps digits optically centred.
                        g.DrawString(text, font, white,
                            new RectangleF(0, size * 0.04f, size, size), format);
                    }
                    images.Add(BitmapToDib(bmp));
                }
            }

            var output = new MemoryStream();
            var writer = new BinaryWriter(output);
            writer.Write((ushort)0);              // idReserved
            writer.Write((ushort)1);              // idType = icon
            writer.Write((ushort)Sizes.Length);   // idCount
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                byte dimension = (byte)(Sizes[i] >= 256 ? 0 : Sizes[i]);
                writer.Write(dimension);           // bWidth
                writer.Write(dimension);           // bHeight
                writer.Write((byte)0);             // bColorCount
                writer.Write((byte)0);             // bReserved
                writer.Write((ushort)1);           // wPlanes
                writer.Write((ushort)32);          // wBitCount
                writer.Write((uint)images[i].Length);
                writer.Write((uint)offset);
                offset += images[i].Length;
            }
            foreach (byte[] data in images) writer.Write(data);
            writer.Flush();
            return output.ToArray();
        }

        // Converts a 32bppArgb Bitmap into a classic ICO DIB image:
        // BITMAPINFOHEADER (height = 2x for XOR + AND masks),
        // bottom-up BGRA XOR pixels, then an all-zero AND mask.
        private static byte[] BitmapToDib(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            int stride = width * 4;
            int maskStride = ((width + 31) / 32) * 4;   // 1bpp rows, DWORD aligned
            int xorSize = stride * height;
            int andSize = maskStride * height;

            var rect = new Rectangle(0, 0, width, height);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            byte[] topDown = new byte[xorSize];
            try
            {
                Marshal.Copy(data.Scan0, topDown, 0, xorSize);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            var output = new MemoryStream();
            var writer = new BinaryWriter(output);
            writer.Write((uint)40);                 // biSize
            writer.Write((int)width);               // biWidth
            writer.Write((int)(height * 2));        // biHeight = XOR + AND
            writer.Write((ushort)1);                // biPlanes
            writer.Write((ushort)32);               // biBitCount
            writer.Write((uint)0);                  // biCompression = BI_RGB
            writer.Write((uint)(xorSize + andSize));
            writer.Write((int)0);                   // biXPelsPerMeter
            writer.Write((int)0);                   // biYPelsPerMeter
            writer.Write((uint)0);                  // biClrUsed
            writer.Write((uint)0);                  // biClrImportant

            // XOR mask: bottom-up rows, already BGRA in memory order.
            for (int row = height - 1; row >= 0; row--)
            {
                writer.Write(topDown, row * stride, stride);
            }
            // AND mask: all zero = fully opaque (alpha lives in the XOR bits).
            writer.Write(new byte[andSize]);
            writer.Flush();
            return output.ToArray();
        }
    }

    // ---------------------------------------------------------------------
    // Logger: one CSV per day, auto purge files older than 30 days
    // ---------------------------------------------------------------------
    internal sealed class Logger
    {
        private readonly object _sync = new object();
        private DateTime _lastCleanup = DateTime.MinValue;

        public void Write(double max, double[] zones, int threshold)
        {
            try
            {
                if (!Directory.Exists(Config.LogDir)) Directory.CreateDirectory(Config.LogDir);
                string file = Path.Combine(Config.LogDir,
                    "temp_" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv");

                var sb = new StringBuilder();
                if (!File.Exists(file))
                    sb.AppendLine("Timestamp,MaxTempC,AllZonesC,ThresholdC,State");

                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                sb.Append(",");
                sb.Append(max.ToString("0.0", CultureInfo.InvariantCulture));
                sb.Append(",\"");
                for (int i = 0; i < zones.Length; i++)
                {
                    if (i > 0) sb.Append("|");
                    sb.Append(zones[i].ToString("0.0", CultureInfo.InvariantCulture));
                }
                sb.Append("\",");
                sb.Append(threshold);
                sb.Append(",");
                sb.Append(max >= threshold ? "WARN" : "OK");
                sb.AppendLine();

                lock (_sync) File.AppendAllText(file, sb.ToString(), new UTF8Encoding(false));
                Cleanup();
            }
            catch { }
        }

        private void Cleanup()
        {
            if ((DateTime.Now - _lastCleanup).TotalHours < 24) return;
            _lastCleanup = DateTime.Now;
            try
            {
                foreach (string path in Directory.GetFiles(Config.LogDir, "temp_*.csv"))
                {
                    if (File.GetLastWriteTime(path) < DateTime.Now.AddDays(-30)) File.Delete(path);
                }
            }
            catch { }
        }
    }

    // ---------------------------------------------------------------------
    // AutoStart: HKCU Run, same value name as the legacy PowerShell script
    // ---------------------------------------------------------------------
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TempGuard";

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\" --minimized"; }
        }

        public static void Set(bool enabled)
        {
            var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(RunKey);
            using (key)
            {
                if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
            }
        }
    }

    // ---------------------------------------------------------------------
    // OverlayForm: click-through floating temperature, top-right corner
    // ---------------------------------------------------------------------
    internal sealed class OverlayForm : Form
    {
        private readonly Label _label;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Width = 112;
            Height = 34;

            _label = new Label();
            _label.Dock = DockStyle.Fill;
            _label.TextAlign = ContentAlignment.MiddleCenter;
            _label.Font = new Font("Consolas", 16f, FontStyle.Bold, GraphicsUnit.Pixel);
            _label.ForeColor = Color.White;
            _label.BackColor = Color.FromArgb(28, 28, 32);
            Controls.Add(_label);
            Place();
        }

        public void Place()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 12, area.Top + 12);
        }

        public void SetTemp(double tempC, bool available, Color color)
        {
            _label.Text = available
                ? tempC.ToString("0.0", CultureInfo.InvariantCulture) + " C"
                : "N/A";
            _label.ForeColor = color;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_LAYERED = 0x00080000;
                const int WS_EX_TRANSPARENT = 0x00000020;
                const int WS_EX_NOACTIVATE = 0x08000000;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
                return cp;
            }
        }
    }

    // ---------------------------------------------------------------------
    // TrayAppContext: the actual application (no main window)
    // ---------------------------------------------------------------------
    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _icon;
        private readonly Timer _timer;
        private readonly Config _config;
        private readonly Logger _logger = new Logger();
        private readonly OverlayForm _overlay;
        private readonly ToolStripMenuItem _menuOverlay;
        private readonly ToolStripMenuItem _menuAutoStart;
        private readonly ToolStripMenuItem _menuThreshold;

        private DateTime _lastAlarm = DateTime.MinValue;
        private string _lastIconKey = "";
        private Icon _currentIcon;

        public TrayAppContext(Config config, bool silent)
        {
            _config = config;
            _overlay = new OverlayForm();

            _icon = new NotifyIcon();
            _icon.Text = "TempGuard";
            _icon.Visible = true;
            _currentIcon = IconFactory.Create(0, _config.ThresholdC, false);
            _icon.Icon = _currentIcon;

            var menu = new ContextMenuStrip();

            _menuThreshold = new ToolStripMenuItem("高温阈值 (Threshold)");
            foreach (int value in new int[] { 70, 75, 80, 85, 90 })
            {
                int captured = value;
                var item = new ToolStripMenuItem(value + " °C");
                item.Click += delegate { SetThreshold(captured); };
                _menuThreshold.DropDownItems.Add(item);
            }
            menu.Items.Add(_menuThreshold);
            menu.Items.Add(new ToolStripSeparator());

            _menuOverlay = new ToolStripMenuItem("显示悬浮温度 (Overlay)");
            _menuOverlay.Click += delegate { ToggleOverlay(); };
            menu.Items.Add(_menuOverlay);

            _menuAutoStart = new ToolStripMenuItem("开机自动启动 (Auto-start)");
            _menuAutoStart.Click += delegate { ToggleAutoStart(); };
            menu.Items.Add(_menuAutoStart);

            var menuLogs = new ToolStripMenuItem("打开日志目录 (Logs)");
            menuLogs.Click += delegate { OpenLogs(); };
            menu.Items.Add(menuLogs);

            var menuSettings = new ToolStripMenuItem("编辑配置文件 (Settings)");
            menuSettings.Click += delegate { OpenSettings(); };
            menu.Items.Add(menuSettings);
            menu.Items.Add(new ToolStripSeparator());

            var menuAbout = new ToolStripMenuItem("关于 (About)");
            menuAbout.Click += delegate { ShowAbout(); };
            menu.Items.Add(menuAbout);

            var menuExit = new ToolStripMenuItem("退出 (Exit)");
            menuExit.Click += delegate { Exit(); };
            menu.Items.Add(menuExit);

            _icon.ContextMenuStrip = menu;
            _icon.DoubleClick += delegate { ToggleOverlay(); };

            _timer = new Timer();
            _timer.Interval = _config.IntervalSeconds * 1000;
            _timer.Tick += delegate { RefreshStatus(); };

            RefreshStatus();
            UpdateMenuChecks();
            if (_config.ShowOverlay) _overlay.Show();
            if (!silent)
            {
                _icon.ShowBalloonTip(3000, "TempGuard",
                    "已在后台运行，托盘图标实时显示温度。右键图标可设置阈值与开机自启。",
                    ToolTipIcon.Info);
            }
            _timer.Start();
        }

        private void RefreshStatus()
        {
            double[] zones = TemperatureReader.Read();
            bool available = zones.Length > 0;
            double max = 0;
            foreach (double zone in zones) if (zone > max) max = zone;
            int rounded = (int)Math.Round(max, MidpointRounding.AwayFromZero);

            if (available && _config.LogEnabled) _logger.Write(max, zones, _config.ThresholdC);

            string key = available ? rounded + "|" + _config.ThresholdC : "na";
            if (key != _lastIconKey)
            {
                Icon next = IconFactory.Create(rounded, _config.ThresholdC, available);
                _icon.Icon = next;
                if (_currentIcon != null) _currentIcon.Dispose();
                _currentIcon = next;
                _lastIconKey = key;
            }

            string tip = available
                ? "TempGuard  |  CPU " + max.ToString("0.0", CultureInfo.InvariantCulture)
                  + " °C  |  阈值 " + _config.ThresholdC + " °C"
                : "TempGuard  |  温度读取失败（尝试以管理员运行）";
            _icon.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;

            _overlay.SetTemp(max, available,
                IconFactory.ColorFor(rounded, _config.ThresholdC, available));

            if (available && max >= _config.ThresholdC &&
                (DateTime.Now - _lastAlarm).TotalSeconds > 30)
            {
                _lastAlarm = DateTime.Now;
                _icon.ShowBalloonTip(5000, "TempGuard 高温警告",
                    "当前 " + max.ToString("0.0", CultureInfo.InvariantCulture) + " °C，"
                    + "已达到阈值 " + _config.ThresholdC + " °C，请检查散热！",
                    ToolTipIcon.Warning);
                SystemSounds.Exclamation.Play();
            }
        }

        private void SetThreshold(int value)
        {
            _config.ThresholdC = value;
            _config.Save();
            _lastIconKey = "";
            RefreshStatus();
            UpdateMenuChecks();
        }

        private void ToggleOverlay()
        {
            _config.ShowOverlay = !_config.ShowOverlay;
            if (_config.ShowOverlay) { _overlay.Place(); _overlay.Show(); }
            else _overlay.Hide();
            _config.Save();
            UpdateMenuChecks();
        }

        private void ToggleAutoStart()
        {
            try { AutoStart.Set(!AutoStart.IsEnabled()); }
            catch { }
            UpdateMenuChecks();
        }

        private void OpenLogs()
        {
            try
            {
                if (!Directory.Exists(Config.LogDir)) Directory.CreateDirectory(Config.LogDir);
                Process.Start("explorer.exe", "\"" + Config.LogDir + "\"");
            }
            catch { }
        }

        private void OpenSettings()
        {
            try
            {
                if (!File.Exists(Config.FilePath)) _config.Save();
                Process.Start("notepad.exe", "\"" + Config.FilePath + "\"");
            }
            catch { }
        }

        private void ShowAbout()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string text =
                "TempGuard " + (version == null ? "1.0" : version.ToString(3)) + "\r\n\r\n" +
                "轻量 Windows 托盘温度监控工具\r\n" +
                "实时温度数字图标 · 高温预警 · 开机自启 · CSV 日志\r\n\r\n" +
                "项目主页：https://github.com/shenge-work/TempGuard\r\n" +
                "项目分类：工具类 / Utilities";
            MessageBox.Show(text, "关于 TempGuard",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateMenuChecks()
        {
            _menuOverlay.Checked = _config.ShowOverlay;
            try { _menuAutoStart.Checked = AutoStart.IsEnabled(); }
            catch { }
            string prefix = _config.ThresholdC.ToString(CultureInfo.InvariantCulture) + " ";
            foreach (ToolStripItem item in _menuThreshold.DropDownItems)
            {
                var menuItem = item as ToolStripMenuItem;
                if (menuItem == null) continue;
                menuItem.Checked = menuItem.Text.StartsWith(prefix);
            }
        }

        private void Exit()
        {
            _timer.Stop();
            _timer.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
            if (_currentIcon != null) _currentIcon.Dispose();
            _overlay.Dispose();
            ExitThread();
        }
    }

    // ---------------------------------------------------------------------
    // Entry point
    // ---------------------------------------------------------------------
    internal static class Program
    {
        private const string MutexName = @"Local\TempGuard.SingleInstance.v1";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew) return;

                var config = Config.Load();
                bool silent = false;
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i].ToLowerInvariant();
                    int number;
                    if (arg == "--interval" && i + 1 < args.Length && int.TryParse(args[i + 1], out number))
                    {
                        config.IntervalSeconds = Math.Max(1, Math.Min(300, number));
                        i++;
                    }
                    else if (arg == "--threshold" && i + 1 < args.Length && int.TryParse(args[i + 1], out number))
                    {
                        config.ThresholdC = Math.Max(40, Math.Min(110, number));
                        i++;
                    }
                    else if (arg == "--overlay") config.ShowOverlay = true;
                    else if (arg == "--no-overlay") config.ShowOverlay = false;
                    else if (arg == "--minimized" || arg == "--silent") silent = true;
                    else if (arg == "--help" || arg == "/?")
                    {
                        MessageBox.Show(
                            "TempGuard 命令行参数：\r\n\r\n" +
                            "--interval N     采样间隔秒数 (1-300)\r\n" +
                            "--threshold N   高温阈值摄氏度 (40-110)\r\n" +
                            "--overlay       显示悬浮温度\r\n" +
                            "--no-overlay    隐藏悬浮温度\r\n" +
                            "--minimized     静默启动（不弹提示）\r\n",
                            "TempGuard 帮助", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                Application.Run(new TrayAppContext(config, silent));
                GC.KeepAlive(mutex);
            }
        }
    }
}
