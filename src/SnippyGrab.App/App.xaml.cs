using System.Windows.Threading;
using SnippyGrab.App.Services;

namespace SnippyGrab.App;

public partial class App : Application
{
    private Mutex? instance;
    private AppController? controller;
    private InstanceActivation? activation;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Native.SetDefaultDllDirectories(0x1000); // Default safe locations; never the working directory.
        if (e.Args.Contains("--check-ui-design"))
        {
            var report = e.Args.SkipWhile(a => a != "--check-ui-design").Skip(1).FirstOrDefault(); if (report is null) { Shutdown(1); return; }
            try { await DesignChecks.Run(report); Shutdown(0); } catch (Exception ex) { File.WriteAllText(report, "FAILED: " + ex); Shutdown(1); }
            return;
        }
        if (e.Args.FirstOrDefault()?.StartsWith("chrome-extension://", StringComparison.Ordinal) == true)
        {
            try { await DesignBrowserBridge.RunHost(e.Args[0]); Shutdown(0); } catch { Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--check-ocr-scheduling"))
        {
            var report = e.Args.SkipWhile(a => a != "--check-ocr-scheduling").Skip(1).FirstOrDefault();
            if (report is null) { Shutdown(1); return; }
            try { await OcrSchedulingChecks.Run(report); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(report, "FAILED: " + ex); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--check-startup-latency"))
        {
            var report = e.Args.SkipWhile(a => a != "--check-startup-latency").Skip(1).FirstOrDefault();
            if (report is null) { Shutdown(1); return; }
            try { await StartupLatencyChecks.Run(report); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(report, "FAILED: " + ex); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--check-transfer-crash"))
        {
            var args = e.Args.SkipWhile(a => a != "--check-transfer-crash").Skip(1).ToArray();
            if (args.Length < 2) { Shutdown(1); return; }
            try
            {
                if (args[0] == "hold") await TransferCrashChecks.Hold(args[1]);
                else if (args[0] == "verify" && args.Length == 3) TransferCrashChecks.Verify(args[1], args[2]);
                else throw new ArgumentException("Expected hold root or verify root report.");
                Shutdown(0);
            }
            catch (Exception ex) { if (args.Length == 3) File.WriteAllText(args[2], "FAILED: " + ex); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--check-resource-stress"))
        {
            var args = e.Args.SkipWhile(a => a != "--check-resource-stress").Skip(1).ToArray();
            if (args.Length == 0) { Shutdown(1); return; }
            try { await ResourceStressChecks.Run(args[0], args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 7200); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(args[0], "FAILED: " + ex); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--check-pipeline-latency"))
        {
            var report = e.Args.SkipWhile(a => a != "--check-pipeline-latency").Skip(1).FirstOrDefault();
            if (report is null) { Shutdown(1); return; }
            try { await PipelineLatencyChecks.Run(report); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(report, "FAILED: " + ex); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--check-editor-performance"))
        {
            var report = e.Args.SkipWhile(a => a != "--check-editor-performance").Skip(1).FirstOrDefault();
            if (report is null) { Shutdown(1); return; }
            try { await EditorPerformanceChecks.Run(report); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(report, "FAILED: " + ex); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--p0-acceptance"))
        {
            var args = e.Args.SkipWhile(a => a != "--p0-acceptance").Skip(1).ToArray();
            if (args.Length is < 1 or > 2) { Shutdown(1); return; }
            controller = P0AcceptanceChecks.Open(args[0], args.Length == 2 ? args[1] : null); return;
        }
        if (e.Args.Contains("--interactive-check"))
        {
            var report = e.Args.SkipWhile(a => a != "--interactive-check").Skip(1).FirstOrDefault();
            if (report is null) { Shutdown(1); return; }
            controller = InteractiveChecks.Open(report); return;
        }
        if (e.Args.Contains("--check-reliability") || e.Args.Contains("--check-overlay-layout") || e.Args.Contains("--check-dock-layout") || e.Args.Contains("--check-editor-layout") || e.Args.Contains("--check-ocr-corpus"))
        {
            var destination = e.Args.SkipWhile(a => a is not ("--check-reliability" or "--check-overlay-layout" or "--check-dock-layout" or "--check-editor-layout" or "--check-ocr-corpus")).Skip(1).FirstOrDefault();
            if (destination is null) { Shutdown(1); return; }
            try
            {
                if (e.Args.Contains("--check-ocr-corpus")) await RuntimeChecks.CheckOcrCorpus(destination);
                else if (e.Args.Contains("--check-reliability")) await RuntimeChecks.CheckReliability(destination);
                else if (e.Args.Contains("--check-dock-layout")) await RuntimeChecks.CheckDockLayout(destination);
                else if (e.Args.Contains("--check-editor-layout")) await RuntimeChecks.CheckEditorLayout(destination);
                else await RuntimeChecks.CheckOverlayLayout(destination);
                Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllText(destination, "FAILED: " + ex); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--self-test") || e.Args.Contains("--benchmark"))
        {
            var destination = e.Args.SkipWhile(a => a is not ("--self-test" or "--benchmark")).Skip(1).FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "SnippyGrab-self-test.json");
            try { if (e.Args.Contains("--benchmark")) await RuntimeChecks.Benchmark(destination); else await RuntimeChecks.Run(destination); Shutdown(0); } catch (Exception ex) { File.WriteAllText(destination, "FAILED: " + ex.ToString()); Shutdown(1); }
            return;
        }
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        activation = new(@"Local\SnippyGrab-activate-" + user);
        instance = new Mutex(true, @"Local\SnippyGrab-" + user, out var created);
        if (!created) { activation.Signal(); Shutdown(); return; }
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnBackgroundUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;
        try
        {
            controller = new(e.Args.Contains("--background"));
            activation.Listen(() =>
            {
                if (Dispatcher.HasShutdownStarted) return;
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (controller is not { Exiting: false }) return;
                        controller.Run(async () => { if (!await controller.OpenDesignInbox()) { controller.Dock.FocusShelf(); if (!controller.Dock.IsVisible) controller.ShowHistory(); } });
                    }));
                }
                catch (InvalidOperationException) when (Dispatcher.HasShutdownStarted) { }
            });
            if (e.Args.Contains("--design-inbox")) await controller.OpenDesignInbox();
        }
        catch (Exception ex) { MessageBox.Show("SnippyGrab could not start. " + OperationFailure.From(ex).Message, "SnippyGrab"); Shutdown(1); }
    }
    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true; controller?.Failure(e.Exception);
    }
    private void OnBackgroundUnhandled(object sender, UnhandledExceptionEventArgs e) => CrashDiagnostics.Record("Unhandled", e.ExceptionObject as Exception);
    private void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e) { CrashDiagnostics.Record("UnobservedTask", e.Exception); e.SetObserved(); }
    protected override void OnExit(ExitEventArgs e)
    {
        activation?.Dispose();
        AppDomain.CurrentDomain.UnhandledException -= OnBackgroundUnhandled;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTask;
        controller?.Dispose(); if (instance is not null) { try { instance.ReleaseMutex(); } catch (ApplicationException) { } instance.Dispose(); }
        base.OnExit(e);
    }
}
