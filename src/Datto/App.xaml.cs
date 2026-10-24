using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
namespace Datto;
public partial class App : Application {
 private Mutex? instance;
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  bool smoke = e.Args.Contains("--smoke-test") || e.Args.Contains("--format-test");
  try {
   if (e.Args.Contains("--format-test")) { FormatChecks.Run(); Shutdown(); return; }
   instance = new Mutex(true, "Local\\Datto.Desktop", out var first);
   if (!first) { if (!smoke) MessageBox.Show("Datto 已在运行，请按 Ctrl + Shift + V 或从托盘打开。", "Datto"); Shutdown(2); return; }
   var window = new MainWindow(smoke);
   MainWindow = window;
   if (smoke && e.Args.Contains("--hidden")) {
    new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
    if (window.IsVisible) throw new InvalidOperationException("Background startup displayed the panel");
    Console.WriteLine("BACKGROUND_OK: window hidden; native handle initialized");
    window.ExitApplication(); return;
   }
   if (smoke) {
    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
    timer.Tick += (_,_) => { timer.Stop(); window.ExitApplication(); };
    window.ContentRendered += (_,_) => { var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(window); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using (var output = File.Create(Path.Combine(AppContext.BaseDirectory, "smoke-preview.png"))) encoder.Save(output); window.VerifyInitialSelection(); Console.WriteLine("STARTUP_OK: window rendered without card selection"); if (e.Args.Contains("--settings")) window.OpenSettings(); timer.Start(); };
   }
   if (!smoke && !e.Args.Contains("--show")) new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle(); else window.Show();
  } catch (Exception ex) {
   var path = Path.Combine(Path.GetTempPath(), "Datto-startup-error.log");
   try { File.WriteAllText(path, ex.ToString()); } catch { }
   Console.Error.WriteLine(ex);
   if (!smoke) MessageBox.Show("启动失败：" + ex.Message + "\n\n诊断日志：" + path, "Datto");
   Shutdown(1);
  }
 }
 protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}







