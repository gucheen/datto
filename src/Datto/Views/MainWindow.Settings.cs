using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
namespace Datto;
public partial class MainWindow {
 Preferences preferences = new();
 int hotkeyId = 1;
 bool smokeMode;
 Window? settingsWindow;
 readonly string preferencesFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Datto", "settings.json");
 const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
 void LoadPreferences(bool smoke) {
  smokeMode = smoke;
  if (!smoke) try { var saved = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(preferencesFile)); if (saved?.Valid == true) preferences = saved; } catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
  ShortcutHint.Text = preferences.Shortcut;
 }
 void TrimHistory() { clips = clips.Where(x => x.Favorite).Concat(clips.Where(x => !x.Favorite).OrderByDescending(x => x.RecentActivity).Take(preferences.HistoryLimit)).OrderByDescending(x => x.RecentActivity).ToList(); }
 void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();
 public void OpenSettings() {
  if (settingsWindow != null) { settingsWindow.Activate(); return; }
  var window = new Window { Title = "Datto · 设置", Width = 570, Height = 650, MinWidth = 510, MinHeight = 580, WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true };
  window.SetResourceReference(BackgroundProperty, "Card"); window.SetResourceReference(ForegroundProperty, "Text");
  var root = new StackPanel { Margin = new Thickness(30,24,30,24) };
  window.Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
  TextBlock Label(string text, int size = 13, string color = "Text") { var label = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) }; label.SetResourceReference(TextBlock.ForegroundProperty, color); return label; }
  TextBox Input(string value) { var input = new TextBox { Text = value, Padding = new Thickness(12,9,12,9), Margin = new Thickness(0,0,0,8), BorderThickness = new Thickness(0) }; input.SetResourceReference(Control.BackgroundProperty,"Input"); input.SetResourceReference(Control.ForegroundProperty,"Text"); input.SetResourceReference(TextBox.CaretBrushProperty,"Text"); return input; }
  void Section(string title) { root.Children.Add(new Border { Height = 1, Margin = new Thickness(0,12,0,16), Background = (System.Windows.Media.Brush)Application.Current.Resources["Edge"] }); root.Children.Add(Label(title,15)); }
  root.Children.Add(Label("设置",26)); root.Children.Add(Label("让 Datto 按你的习惯工作。",12,"Muted"));
  Section("启动与快捷键");
  var autoStart = new CheckBox { Content = "登录 Windows 时启动 Datto", Margin = new Thickness(0,2,0,7) }; autoStart.SetResourceReference(Control.ForegroundProperty,"Text"); root.Children.Add(autoStart);
  var note = Label("启动后安静驻留托盘，不弹出剪贴板面板。",11,"Muted"); root.Children.Add(note);
  object? originalRun = null;
  try { if (!smokeMode) { using var key = Registry.CurrentUser.OpenSubKey(RunKey); originalRun = key?.GetValue("Datto"); } autoStart.IsChecked = originalRun != null; }
  catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { autoStart.IsEnabled = false; note.Text = "无法读取 Windows 启动设置。"; }
  root.Children.Add(Label("唤起快捷键",12));
  uint modifiers = preferences.Modifiers, virtualKey = preferences.Key;
  var shortcut = Input(preferences.Shortcut); shortcut.IsReadOnly = true; shortcut.ToolTip = "点击后按下新组合键：Ctrl / Alt / Shift + 字母、数字或 F1–F12"; root.Children.Add(shortcut);
  root.Children.Add(Label("点击输入框后按组合键；至少包含 Ctrl、Alt、Shift 中的一项。",11,"Muted"));
  var feedback = Label("",12,"Muted");
  shortcut.PreviewKeyDown += (_,e) => {
   if (e.Key == System.Windows.Input.Key.Tab) return;
   e.Handled = true;
   var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
   var vk = (uint)KeyInterop.VirtualKeyFromKey(key); var m = Keyboard.Modifiers;
   uint mods = (m.HasFlag(ModifierKeys.Control) ? 2u : 0u) | (m.HasFlag(ModifierKeys.Alt) ? 1u : 0u) | (m.HasFlag(ModifierKeys.Shift) ? 4u : 0u);
   var candidate = new Preferences { Modifiers = mods, Key = vk };
   if (!candidate.Valid || m.HasFlag(ModifierKeys.Windows)) { feedback.Text = "请使用修饰键 + 字母、数字或 F1–F12。"; return; }
   modifiers = mods; virtualKey = vk; shortcut.Text = candidate.Shortcut; feedback.Text = "保存时检查快捷键是否被占用。";
  };
  Section("监听与排除应用");
  var pause = new Button { Content = paused ? "恢复监听" : "暂停监听", Padding = new Thickness(12,8,12,8), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,0,0,10) };
  pause.SetResourceReference(Control.BackgroundProperty,"Tab"); pause.SetResourceReference(Control.ForegroundProperty,"Text");
  pause.Click += (_,_) => { ToggleListening(); pause.Content = paused ? "恢复监听" : "暂停监听"; }; root.Children.Add(pause);
  root.Children.Add(Label("排除应用（进程名，每行一个）",12));
  var exclusions = Input(string.Join(Environment.NewLine, preferences.ExcludedApps ?? Array.Empty<string>())); exclusions.AcceptsReturn = true; exclusions.Height = 75; exclusions.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; root.Children.Add(exclusions);
  root.Children.Add(Label("例如 KeePass.exe。按剪贴板来源进程匹配，不区分大小写；保存后生效，不删除已有历史。",11,"Muted"));
  Section("历史记录"); root.Children.Add(Label("最大普通记录数量",12));
  var limit = Input(preferences.HistoryLimit.ToString()); root.Children.Add(limit);
  root.Children.Add(Label("50–10,000 条。收藏不计入上限；超出时移除最旧的普通记录。",11,"Muted"));
  var keepFavorites = new CheckBox { Content = "清空时保留收藏", IsChecked = true, Margin = new Thickness(0,6,0,10) }; keepFavorites.SetResourceReference(Control.ForegroundProperty,"Text"); root.Children.Add(keepFavorites);
  var clear = new Button { Content = "清空历史…", Padding = new Thickness(12,8,12,8), HorizontalAlignment = HorizontalAlignment.Left }; clear.SetResourceReference(Control.BackgroundProperty,"Tab"); clear.SetResourceReference(Control.ForegroundProperty,"Text"); clear.Click += (_,_) => ClearHistory(keepFavorites.IsChecked == true, window); root.Children.Add(clear);
  Section("数据统计");
  var stats = Label("",13); root.Children.Add(stats);
  var storage = Label("",11,"Muted"); root.Children.Add(storage);
  void UpdateStats() {
   stats.Text = $"{clips.Count:N0} 条记录     ·     {clips.Count(x => x.Favorite):N0} 条收藏     ·     {clips.Count(x => x.IsLink):N0} 条链接";
   long bytes = System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(clips));
   storage.Text = $"当前历史序列化大小：{bytes / 1024.0:N1} KB\n统计当前保留的数据，不包含已删除记录。";
  }
  UpdateStats();
  var statsTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; statsTimer.Tick += (_,_) => UpdateStats(); statsTimer.Start();
  root.Children.Add(feedback);
  var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,8,0,0) };
  var cancel = new Button { Content = "关闭", Padding = new Thickness(20,9,20,9), Margin = new Thickness(0,0,10,0) };
  var save = new Button { Content = "保存设置", Padding = new Thickness(20,9,20,9) };
  foreach (var b in new[] { cancel, save }) { b.SetResourceReference(Control.BackgroundProperty,"Tab"); b.SetResourceReference(Control.ForegroundProperty,"Text"); b.BorderThickness = new Thickness(0); buttons.Children.Add(b); }
  root.Children.Add(buttons); cancel.Click += (_,_) => window.Close();
  save.Click += (_,_) => {
   if (smokeMode) { feedback.Text = "测试模式不写入设置。"; return; }
   if (!int.TryParse(limit.Text, out var count) || count < 50 || count > 10000) { feedback.Text = "记录数量必须为 50–10,000 的整数。"; return; }
   int removed = Math.Max(0, clips.Count(x => !x.Favorite) - count);
   if (removed > 0 && MessageBox.Show(window, $"新的上限会移除最旧的 {removed} 条普通记录，收藏保留。是否继续？", "调整历史上限", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
   var excluded = exclusions.Text.Split(new[] {'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(NormalizeProcess).Where(x => x.Length > 0).Distinct().ToArray();
   if (excluded.Any(x => x.IndexOfAny(new[] {'\\','/',':','*','?'}) >= 0)) { feedback.Text = "排除列表请填写进程名，不要填写路径或通配符。"; return; }
   var next = new Preferences { HistoryLimit = count, Modifiers = modifiers, Key = virtualKey, ExcludedApps = excluded };
   bool changeHotkey = modifiers != preferences.Modifiers || virtualKey != preferences.Key;
   int nextId = hotkeyId == 1 ? 2 : 1;
   if (changeHotkey && !RegisterHotKey(handle, nextId, modifiers | 0x4000, virtualKey)) { feedback.Text = "快捷键已被占用，原快捷键保持有效。"; return; }
   bool changedRun = false;
   try {
    if (autoStart.IsEnabled && (autoStart.IsChecked == true) != (originalRun != null)) {
     using var key = Registry.CurrentUser.CreateSubKey(RunKey);
     if (autoStart.IsChecked == true) {
      var exe = Path.Combine(AppContext.BaseDirectory,"Datto.exe");
      if (!File.Exists(exe)) throw new IOException("找不到 Datto.exe，无法设置开机启动。");
      key.SetValue("Datto", "\"" + exe + "\" --background");
     } else key.DeleteValue("Datto", false);
     changedRun = true;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(preferencesFile)!);
    File.WriteAllText(preferencesFile + ".tmp", JsonSerializer.Serialize(next)); File.Move(preferencesFile + ".tmp", preferencesFile, true);
   } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
    if (changeHotkey) UnregisterHotKey(handle, nextId);
    if (changedRun) try { using var key = Registry.CurrentUser.CreateSubKey(RunKey); if (originalRun == null) key.DeleteValue("Datto",false); else key.SetValue("Datto",originalRun); } catch { feedback.Text = "保存失败且启动项恢复失败，请检查 Windows 启动设置。"; return; }
    feedback.Text = "保存失败：" + ex.Message; return;
   }
   if (changeHotkey) { UnregisterHotKey(handle, hotkeyId); hotkeyId = nextId; }
   preferences = next; ShortcutHint.Text = next.Shortcut;
   TrimHistory(); Save(); Refresh(); UpdateStats();
   if (changedRun) originalRun = autoStart.IsChecked == true ? "\"" + Path.Combine(AppContext.BaseDirectory,"Datto.exe") + "\" --background" : null;
   feedback.Text = "设置已保存，即刻生效。";
  };
  window.Deactivated += (_,_) => QueueAutoHide();
  window.Closed += (_,_) => { statsTimer.Stop(); settingsWindow = null; };
  if (smokeMode) window.ContentRendered += (_,_) => {
   var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
   bitmap.Render(window); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
   using var output = File.Create(Path.Combine(AppContext.BaseDirectory, "settings-preview.png")); encoder.Save(output);
   if (!new Preferences().Valid || new Preferences { HistoryLimit = 49 }.Valid || new Preferences { HistoryLimit = 10001 }.Valid || new Preferences { Modifiers = 0 }.Valid) throw new InvalidOperationException("Settings validation regression");
   if (NormalizeProcess(" KeePass.EXE ") != "keepass" || QuickIndex(Key.D1) != 0 || QuickIndex(Key.NumPad9) != 8 || QuickIndex(Key.D0) != -1) throw new InvalidOperationException("Clipboard action regression");
   Console.WriteLine("SETTINGS_OK: rendered; settings, exclusions and digit mapping checked");
  };
  settingsWindow = window; window.Show();
 }
}



