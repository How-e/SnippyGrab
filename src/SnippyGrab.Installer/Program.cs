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
        var form = new SetupForm { Text = "SnippyGrab Setup", ClientSize = new Size(560, 390), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, StartPosition = FormStartPosition.CenterScreen, Font = new Font("Segoe UI", 10), Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) };
        var title = new Label { Text = "Install SnippyGrab", Font = new Font("Segoe UI", 22), AutoSize = true, Location = new Point(24, 22) };
        var details = new Label { Text = $"{version}\n\nA local screenshot shelf. No account or uploads.\n\nInstalls for this Windows user without administrator access.\nCaptures and settings stay separate from application files.\nUninstall through Windows Apps; user data is retained.\n\nUnsigned build: Windows may show a SmartScreen prompt.\nOCR may require the Microsoft Visual C++ x64 runtime.", Location = new Point(24, 78), Size = new Size(515, 220) };
        var startup = new CheckBox { Text = "Launch at Windows login", AutoSize = true, Location = new Point(24, 304) };
        var status = new Label { Text = "", Location = new Point(24, 337), Size = new Size(345, 42) };
        var install = new Button { Text = "Install", Location = new Point(399, 333), Size = new Size(135, 34) };
        var completed = false;
        form.FormClosing += (_, e) => { if (!install.Enabled && !completed) { e.Cancel = true; status.Text = "Installation is in progress; wait for the result."; } };
        install.Click += async (_, _) =>
        {
            if (completed) { form.Close(); return; }
            var startAtLogin = startup.Checked;
            install.Enabled = false; startup.Enabled = false; status.Text = "Verifying and installing…";
            try { await Task.Run(() => Install(startAtLogin)); status.Text = "Installed. Open SnippyGrab from Start."; completed = true; install.Text = "Close"; install.Enabled = true; }
            catch (Exception ex) { status.Text = "Install failed. Exit any running SnippyGrab and retry."; MessageBox.Show(form, ex.Message, "SnippyGrab Setup"); install.Enabled = true; startup.Enabled = true; }
        };
        form.Controls.AddRange([title, details, startup, status, install]); form.AcceptButton = install; return form;
    }
    private sealed class SetupForm : Form
    {
        internal bool Diagnostic;
        protected override bool ShowWithoutActivation => Diagnostic;
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
