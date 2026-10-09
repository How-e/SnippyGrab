using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SnippyGrab.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args is ["--verify-payload", var report])
        {
            try { using var archive = OpenArchive(); Validate(archive); File.WriteAllText(report, "PASS: embedded payload paths and SHA256SUMS verified."); return 0; }
            catch (Exception ex) { File.WriteAllText(report, "FAILED: " + ex); return 1; }
        }
        if (args is ["--verify-archive", var path, var result])
        {
            try { using var archive = ZipFile.OpenRead(path); Validate(archive); File.WriteAllText(result, "PASS"); return 0; }
            catch (Exception ex) { File.WriteAllText(result, "FAILED: " + ex.GetType().Name); return 1; }
        }
        var form = CreateForm();
        if (args is ["--check-layout", var image])
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000);
            ((SetupForm)form).Diagnostic = true; form.Show(); form.Update(); form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(image); form.Close(); form.Dispose(); return 0;
        }
        Application.Run(form); return 0;
    }
    private static ZipArchive OpenArchive() => new(Assembly.GetExecutingAssembly().GetManifestResourceStream("SnippyGrab.Bundle.zip") ?? throw new InvalidOperationException("Installer payload unavailable. Build using scripts/package.ps1."), ZipArchiveMode.Read);
    private static void Validate(ZipArchive archive)
    {
        if (archive.Entries.Count is < 5 or > 200 || archive.Entries.Sum(e => e.Length) > 512L * 1024 * 1024) throw new InvalidDataException("Invalid bundle count or total size.");
        var files = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.Split('/').Any(part => part is "" or "." or "..") || entry.FullName.Contains(':') || entry.FullName.Contains('\\') || entry.Length > 512L * 1024 * 1024 || !files.TryAdd(entry.FullName, entry)) throw new InvalidDataException("Invalid bundle path or size.");
        }
        if (!files.ContainsKey("SnippyGrab.exe") || !files.ContainsKey("install.ps1") || !files.TryGetValue("SHA256SUMS.txt", out var manifest)) throw new InvalidDataException("Incomplete bundle.");
        using var reader = new StreamReader(manifest.Open());
        var checks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length < 67 || line.Substring(64, 2) != "  ") throw new InvalidDataException("Invalid checksum record.");
            var path = line[66..].Replace('\\', '/');
            if (!checks.Add(path) || !files.TryGetValue(path, out var entry)) throw new InvalidDataException("Unknown checksum path.");
            using var input = entry.Open();
            if (!Convert.ToHexString(SHA256.HashData(input)).Equals(line[..64], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Bundle checksum mismatch.");
        }
        if (checks.Count != files.Count - 1 || checks.Contains("SHA256SUMS.txt")) throw new InvalidDataException("Incomplete checksum coverage.");
    }
    private static Form CreateForm()
    {
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "development";
        var identity = version.Split('+', 2);
        version = identity[0] + (identity.Length == 2 ? " · " + identity[1][..Math.Min(8, identity[1].Length)] : "");
        var form = new SetupForm { Text = "SnippyGrab Setup", ClientSize = new Size(720, 570), MinimumSize = new Size(600, 510), StartPosition = FormStartPosition.CenterScreen, Font = new Font("Segoe UI", 10.5f), Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Margin = Padding.Empty };
        var title = new Label { Text = "Install SnippyGrab", Font = new Font("Segoe UI", 25, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        content.Controls.Add(title);
        content.Controls.Add(new Label { Text = version, AutoSize = true, Margin = new Padding(0, 0, 0, 8), Tag = "muted" });
        content.Controls.Add(new Label { Text = "A local screenshot shelf. No accounts or uploads.", AutoSize = true, Margin = new Padding(0, 0, 0, 20), Tag = "muted" });
        var data = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 14), Tag = "group", CellBorderStyle = TableLayoutPanelCellBorderStyle.None };
        data.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); data.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        static Control Info(string heading, string body)
        {
            var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 12, 0) };
            panel.Controls.Add(new Label { Text = heading, AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6) });
            panel.Controls.Add(new Label { Text = body, AutoSize = true, Tag = "muted", Margin = Padding.Empty }); return panel;
        }
        data.Controls.Add(Info("Installs for your Windows user", "No administrator access required."), 0, 0);
        data.Controls.Add(Info("Your data stays separate", "Uninstall keeps captures and settings."), 1, 0); content.Controls.Add(data);
        var before = Info("Before you install", "This build is unsigned. Windows may show a SmartScreen prompt.\nOCR may require the Microsoft Visual C++ x64 runtime.");
        before.Padding = new Padding(16); before.Margin = new Padding(0, 0, 0, 14); before.Tag = "group"; content.Controls.Add(before);
        var startup = new CheckBox { Text = "Launch at Windows login", AutoSize = true, Margin = Padding.Empty, AccessibleName = "Launch at Windows login" };
        var startupGroup = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 14), Tag = "group" }; startupGroup.Controls.Add(startup); content.Controls.Add(startupGroup);
        var footer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 14, 0, 0), Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var status = new Label { Text = "Ready to install", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted", AccessibleName = "Installation status" };
        var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(98, 38), Margin = new Padding(6, 0, 6, 0), FlatStyle = FlatStyle.Flat };
        var install = new Button { Text = "Install", AutoSize = true, MinimumSize = new Size(118, 38), Margin = Padding.Empty, FlatStyle = FlatStyle.Flat, Tag = "primary" };
        cancel.Click += (_, _) => form.Close();
        footer.Controls.Add(status, 0, 0); footer.Controls.Add(cancel, 1, 0); footer.Controls.Add(install, 2, 0);
        root.Controls.Add(content, 0, 0); root.Controls.Add(footer, 0, 1); form.Controls.Add(root);
        void ResizeContent()
        {
            var width = Math.Max(280, content.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            foreach (Control control in content.Controls) { control.Width = width; if (control.Tag as string == "group") control.MinimumSize = new Size(width, 0); if (control is Label label) label.MaximumSize = new Size(width, 0); }
            foreach (Control panel in new[] { data, before })
                foreach (Control inner in panel.Controls) { var labelWidth = panel == data ? Math.Max(130, (width - 48) / 2) : width - 32; if (inner is Label label) label.MaximumSize = new Size(labelWidth, 0); else foreach (Control child in inner.Controls) if (child is Label text) text.MaximumSize = new Size(labelWidth, 0); }
        }
        content.SizeChanged += (_, _) => ResizeContent(); form.Shown += (_, _) => ResizeContent(); ResizeContent();
        form.ApplyTheme();
        var completed = false;
        form.FormClosing += (_, e) => { if (!install.Enabled && !completed) { e.Cancel = true; status.Text = "Installation is in progress; wait for the result."; } };
        install.Click += async (_, _) =>
        {
            if (completed) { form.Close(); return; }
            var startAtLogin = startup.Checked;
            install.Enabled = false; startup.Enabled = false; cancel.Enabled = false; status.Text = "Verifying and installing…";
            try { await Task.Run(() => Install(startAtLogin)); status.Text = "Installed. Open SnippyGrab from Start."; completed = true; cancel.Visible = false; install.Text = "Close"; install.Enabled = true; }
            catch (Exception ex)
            {
                status.Text = "Install failed. Review the error details and retry."; using var error = new SetupForm { Text = "Installation details", Size = new Size(600, 330), MinimumSize = new Size(420, 250), Font = form.Font, StartPosition = FormStartPosition.CenterParent };
                var details = new TextBox { Text = ex.Message, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
                var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 38, FlatStyle = FlatStyle.Flat }; close.Click += (_, _) => error.Close(); error.Padding = new Padding(20); error.Controls.Add(details); error.Controls.Add(close); error.ApplyTheme(); error.ShowDialog(form);
                install.Enabled = true; startup.Enabled = true; cancel.Enabled = true;
            }
        };
        form.AcceptButton = install; form.CancelButton = cancel; return form;
    }
    private sealed class SetupForm : Form
    {
        internal bool Diagnostic;
        protected override bool ShowWithoutActivation => Diagnostic;
        private readonly Dictionary<Control, string> roles = [];
        internal void ApplyTheme()
        {
            var dark = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0;
            Color Token(string light, string night) => ColorTranslator.FromHtml(dark ? night : light);
            var shell = SystemInformation.HighContrast ? SystemColors.Window : Token("#F7F7F8", "#1C1D20");
            var raised = SystemInformation.HighContrast ? SystemColors.Window : Token("#FFFFFF", "#252629");
            var ink = SystemInformation.HighContrast ? SystemColors.WindowText : Token("#202124", "#F1F2F4");
            var muted = SystemInformation.HighContrast ? SystemColors.WindowText : Token("#60646C", "#B2B5BD");
            var border = SystemInformation.HighContrast ? SystemColors.WindowText : Token("#D4D5D8", "#44464D");
            void Apply(Control control)
            {
                var role = control.Tag as string ?? ""; control.BackColor = role == "group" ? raised : control.Parent?.BackColor ?? shell; control.ForeColor = role == "muted" ? muted : ink;
                if (control is Button button) { button.BackColor = role == "primary" ? SystemInformation.HighContrast ? SystemColors.Highlight : ColorTranslator.FromHtml("#465F84") : raised; button.ForeColor = role == "primary" ? SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White : ink; button.FlatAppearance.BorderColor = border; button.FlatAppearance.MouseOverBackColor = role == "primary" ? button.BackColor : Token("#ECEDEF", "#303238"); }
                if (role == "group" && roles.TryAdd(control, role)) control.Paint += (_, e) =>
                {
                    var diameter = Math.Min(16f * control.DeviceDpi / 96, Math.Min(control.Width - 1, control.Height - 1));
                    if (diameter <= 0) return;
                    using var outline = new System.Drawing.Drawing2D.GraphicsPath();
                    outline.AddArc(0.5f, 0.5f, diameter, diameter, 180, 90);
                    outline.AddArc(control.Width - diameter - 0.5f, 0.5f, diameter, diameter, 270, 90);
                    outline.AddArc(control.Width - diameter - 0.5f, control.Height - diameter - 0.5f, diameter, diameter, 0, 90);
                    outline.AddArc(0.5f, control.Height - diameter - 0.5f, diameter, diameter, 90, 90); outline.CloseFigure();
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.Clear(control.Parent?.BackColor ?? BackColor);
                    using var fill = new SolidBrush(control.BackColor); e.Graphics.FillPath(fill, outline);
                    using var pen = new Pen(SystemInformation.HighContrast ? SystemColors.WindowText : ColorTranslator.FromHtml(BackColor.GetBrightness() < 0.5 ? "#44464D" : "#D4D5D8")); e.Graphics.DrawPath(pen, outline);
                };
                foreach (Control child in control.Controls) Apply(child);
            }
            BackColor = shell; ForeColor = ink; foreach (Control child in Controls) Apply(child);
            if (IsHandleCreated) { var useDark = dark && !SystemInformation.HighContrast ? 1 : 0; DwmSetWindowAttribute(Handle, 20, ref useDark, sizeof(int)); }
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTheme(); }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    }
    private static void Install(bool startup)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (var archive = OpenArchive()) { Validate(archive); archive.ExtractToDirectory(root); }
            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(root, "install.ps1") }) info.ArgumentList.Add(argument);
            if (startup) info.ArgumentList.Add("-Startup");
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Installer process unavailable.");
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); process.WaitForExit();
            Task.WaitAll(output, error);
            if (process.ExitCode != 0) throw new InvalidOperationException("Installation failed: " + error.Result);
        }
        finally { Directory.Delete(root, true); }
    }
}
