using System.Windows.Interop;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class PinWindow : Window
{
    private readonly AppController controller;
    private readonly CaptureRecord record;
    private readonly Image preview;
    private readonly CaptureViewLease lease;
    public PinWindow(AppController controller, CaptureRecord record)
    {
        Ui.StyleWindow(this);
        this.controller = controller; this.record = record;
        Title = "SnippyGrab pin"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; Width = 320; Height = 220; MinWidth = 80; MinHeight = 60; ResizeMode = ResizeMode.CanResizeWithGrip;
        preview = new Image { Source = ImageService.Load(controller.Repository.PathFor(record), 800), Stretch = Stretch.Uniform };
        Content = new Border { Child = preview, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = (Brush)FindResource("Muted"), Background = (Brush)FindResource("Surface") };
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) controller.Edit(record); else DragMove(); };
        var menu = new ContextMenu();
        foreach (var (label, action) in new (string, Action)[]
        {
            ("Copy", () => controller.Run(() => controller.Copy(record))), ("Edit", () => controller.Edit(record)),
            ("Toggle always on top", () => Topmost = !Topmost), ("Opacity 50% / 100%", () => Opacity = Opacity < 1 ? 1 : 0.5),
            ("Click-through (restore via tray → Restore pins)", () => { var hwnd = new WindowInteropHelper(this).Handle; Native.SetWindowLongPtr(hwnd, -20, Native.GetWindowLongPtr(hwnd, -20) | 0x20 | 0x08000000); }),
            ("Return to shelf", () => { record.Dismissed = false; controller.Repository.Persist(); controller.Dock.Reveal(); Close(); }), ("Close pin window", Close)
        }) { var item = new MenuItem { Header = label }; item.Click += (_, _) => controller.Try(action); menu.Items.Add(item); }
        ContextMenu = menu;
        lease = new(controller.Repository, record);
        controller.Repository.RevisionChanged += RevisionChanged;
        Closed += (_, _) => { controller.Repository.RevisionChanged -= RevisionChanged; lease.Dispose(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
    private void RevisionChanged(CaptureRecord changed)
    {
        if (changed.Id == record.Id) controller.Try(() => preview.Source = ImageService.Load(controller.Repository.PathFor(record), 800));
    }
    public void RestoreInteraction()
    {
        var hwnd = new WindowInteropHelper(this).Handle; Native.SetWindowLongPtr(hwnd, -20, Native.GetWindowLongPtr(hwnd, -20) & ~((nint)0x20 | 0x08000000));
        Show(); Activate(); preview.Source = ImageService.Load(controller.Repository.PathFor(record), 800);
    }
}
