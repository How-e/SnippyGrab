using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SnippyGrab.App.Services;

internal static class DesignBrowserBridge
{
    internal const string HostName = "com.snippygrab.design";
    internal static string DataRoot => ManagedPath.StorageRoot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnippyGrab"));
    internal static string Inbox(string root) => Path.Combine(root, "design-inbox");
    internal static void ShowSetup(Window owner)
    {
        var window = new Window { Title = "SnippyGrab · Browser extension", Width = 650, Height = 430, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner }; Views.Ui.StyleWindow(window);
        var panel = new StackPanel { Margin = new Thickness(22) }; window.Content = Views.Ui.Scroll(panel);
        panel.Children.Add(Views.Ui.Heading("Import an existing browser tab", 23));
        panel.Children.Add(Views.Ui.Text("1. Open chrome://extensions or edge://extensions. Enable Developer mode and choose Load unpacked.\n2. Select the Design folder beside SnippyGrab.exe.\n3. Paste that extension's ID below and register this installation.\n4. Open your page and use the SnippyGrab extension to capture its visible area or pick an element.", 14));
        var id = new TextBox { MaxLength = 32 }; System.Windows.Automation.AutomationProperties.SetName(id, "Browser extension ID"); panel.Children.Add(Views.Ui.Text("Extension ID")); panel.Children.Add(id);
        var status = Views.Ui.Text("The bridge is optional. Registration changes only your user profile.", 12, true);
        panel.Children.Add(Views.Ui.Button("Open extension folder", "Open the bundled browser extension folder", () => Process.Start(new ProcessStartInfo { FileName = DesignBrowser.AssetsRoot, UseShellExecute = true })));
        panel.Children.Add(Views.Ui.Button("Register browser bridge", "Register this extension for Chrome and Edge", () => { try { Register(DataRoot, id.Text.Trim(), Environment.ProcessPath!); status.Text = "Registered. Use the extension on a page you want to edit. Re-register after moving this installation."; } catch (Exception ex) { status.Text = OperationFailure.From(ex).Message; } }));
        panel.Children.Add(Views.Ui.Button("Remove bridge registration", "Remove SnippyGrab's Chrome and Edge native messaging registration", () => { Unregister(DataRoot); status.Text = "Registration removed. URL import remains available."; })); panel.Children.Add(status); window.ShowDialog();
    }
    internal static void Register(string root, string id, string executable)
    {
        if (!Regex.IsMatch(id, "^[a-p]{32}$") || !Path.IsPathFullyQualified(executable) || !File.Exists(executable)) throw new InvalidDataException("Enter the 32-letter extension ID shown by your browser.");
        var folder = Path.Combine(root, "design-bridge"); ManagedPath.RejectRedirects(folder); Directory.CreateDirectory(folder);
        var manifest = Path.Combine(folder, "host.json"); var origins = new HashSet<string>(StringComparer.Ordinal) { "chrome-extension://" + id + "/" };
        if (File.Exists(manifest) && new FileInfo(manifest).Length <= 4096)
        {
            using var existing = JsonDocument.Parse(File.ReadAllBytes(manifest));
            foreach (var origin in existing.RootElement.GetProperty("allowed_origins").EnumerateArray().Select(x => x.GetString() ?? "")) if (Regex.IsMatch(origin, "^chrome-extension://[a-p]{32}/$") && origins.Count < 8) origins.Add(origin);
        }
        AtomicFile.Write(manifest, JsonSerializer.SerializeToUtf8Bytes(new { name = HostName, description = "SnippyGrab UI Design import", path = executable, type = "stdio", allowed_origins = origins.Order(StringComparer.Ordinal).ToArray() }));
        foreach (var prefix in new[] { @"Software\Google\Chrome\NativeMessagingHosts\", @"Software\Microsoft\Edge\NativeMessagingHosts\" }) using (var key = Registry.CurrentUser.CreateSubKey(prefix + HostName)) key.SetValue("", manifest);
    }
    internal static void Unregister(string root)
    {
        var manifest = Path.Combine(root, "design-bridge", "host.json");
        foreach (var prefix in new[] { @"Software\Google\Chrome\NativeMessagingHosts\", @"Software\Microsoft\Edge\NativeMessagingHosts\" })
        {
            using var key = Registry.CurrentUser.OpenSubKey(prefix + HostName);
            if (string.Equals(key?.GetValue("") as string, manifest, StringComparison.OrdinalIgnoreCase)) Registry.CurrentUser.DeleteSubKey(prefix + HostName, false);
        }
    }
    internal static bool Authorized(string root, string origin)
    {
        if (!Regex.IsMatch(origin, "^chrome-extension://[a-p]{32}/$")) return false;
        var path = Path.Combine(root, "design-bridge", "host.json"); ManagedPath.RejectRedirects(path);
        if (!File.Exists(path) || new FileInfo(path).Length > 4096) return false;
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(path)); return manifest.RootElement.GetProperty("allowed_origins").EnumerateArray().Any(x => x.GetString() == origin);
    }
    internal static async Task RunHost(string origin)
    {
        using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
        try
        {
            if (!Authorized(DataRoot, origin)) throw new InvalidDataException("This browser extension is not registered.");
            var payload = await ReadMessage(input); using var json = JsonDocument.Parse(payload);
            if (json.RootElement.GetProperty("command").GetString() != "capture") throw new InvalidDataException("Unsupported browser command.");
            var capture = json.RootElement.GetProperty("capture").GetRawText(); var screenshot = json.RootElement.GetProperty("screenshot").GetString()!;
            if (!screenshot.StartsWith("data:image/png;base64,", StringComparison.Ordinal)) throw new InvalidDataException("Expected a PNG reference.");
            // Validate dimensions and the scene before admitting a durable inbox file.
            var png = Convert.FromBase64String(screenshot[22..]);
            await ImageWorker.RunAsync(() => DesignBrowser.FromCapture(capture, png));
            var inbox = Inbox(DataRoot); ManagedPath.RejectRedirects(inbox); Directory.CreateDirectory(inbox);
            if (Directory.EnumerateFiles(inbox, "*.json").Count() >= 10) throw new InvalidDataException("The design inbox is full. Open SnippyGrab to review pending captures.");
            AtomicFile.Write(Path.Combine(inbox, Guid.NewGuid().ToString("N") + ".json"), payload);
            Process.Start(new ProcessStartInfo { FileName = Environment.ProcessPath!, Arguments = "--design-inbox", UseShellExecute = false, CreateNoWindow = true });
            await WriteMessage(output, new { ok = true });
        }
        catch (Exception ex) { await WriteMessage(output, new { ok = false, error = OperationFailure.From(ex).Message }); }
    }
    internal static async Task<byte[]> ReadMessage(Stream input)
    {
        var header = new byte[4]; await input.ReadExactlyAsync(header); var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length is 0 or > 64 * 1024 * 1024) throw new InvalidDataException("Browser import exceeds 64 MiB.");
        var bytes = new byte[(int)length]; await input.ReadExactlyAsync(bytes); return bytes;
    }
    private static async Task WriteMessage(Stream output, object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value); var header = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)bytes.Length); await output.WriteAsync(header); await output.WriteAsync(bytes); await output.FlushAsync();
    }
}
