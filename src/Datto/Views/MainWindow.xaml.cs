using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace Datto;
public partial class MainWindow : Window {
 [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
 readonly string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Datto", "history.json");
 List<Clip> clips = new();
 string filter = "all", last = "";
 bool exiting; Window? previewWindow;
 IntPtr handle;
 HwndSource? source;
 Forms.NotifyIcon? tray;
 readonly DispatcherTimer retry = new() { Interval = TimeSpan.FromMilliseconds(100) };
 int attempts;
 public MainWindow(bool smoke = false) {
  InitializeComponent();
  Deactivated += (_,_) => QueueAutoHide();
  InitializeTheme(smoke);
  LoadPreferences(smoke);
  Loaded += (_,_) => { PositionPanel(); Search.Focus(); };
  try { if (File.Exists(file)) clips = (JsonSerializer.Deserialize<List<Clip>>(File.ReadAllText(file)) ?? new()).Where(x => x != null && x.HasContent).ToList(); }
  catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { Status.Text = "历史记录读取失败，原文件已保留。"; }
    if (smoke) {
   var sample = new DataObject(); sample.SetImage(System.Windows.Media.Imaging.BitmapSource.Create(2, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[] { 70,160,220,255, 180,130,70,255 }, 8));
   clips = new List<Clip> { ClipFormats.Read(sample)!, new() { Files = new[] { @"C:\Examples\report.pdf", @"C:\Examples\photo.jpg" } }, new() { Text = "富文本预览 · 保留 HTML / RTF 格式", Html = "<b>富文本预览</b>" }, new() { Text = "Startup regression test", Favorite = true }, new() { Text = "https://example.com" } };
  }
  Refresh();
  retry.Tick += (_,_) => { retry.Stop(); Capture(); };
  SourceInitialized += (_,_) => {
   handle = new WindowInteropHelper(this).Handle;
   source = HwndSource.FromHwnd(handle); source.AddHook(Hook);
   if (smoke) return;
   if (!AddClipboardFormatListener(handle)) Status.Text = "无法注册剪贴板监听，请重新启动。";
   if (!RegisterHotKey(handle, hotkeyId, preferences.Modifiers | 0x4000, preferences.Key)) Status.Text = preferences.Shortcut + " 已被其他应用占用，可从托盘打开设置。";
   var menu = new Forms.ContextMenuStrip();
   menu.Items.Add("打开 Datto", null, (_,_) => Reveal());
   menu.Items.Add("设置…", null, (_,_) => OpenSettings());
   pauseMenu = new Forms.ToolStripMenuItem("暂停监听"); pauseMenu.Click += (_,_) => ToggleListening(); menu.Items.Add(pauseMenu);
   AddThemeMenu(menu);
   menu.Items.Add("退出", null, (_,_) => { exiting = true; Close(); Application.Current.Shutdown(); });
   tray = new Forms.NotifyIcon { Text = "Datto · 剪贴板", Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Datto.ico")).Stream), ContextMenuStrip = menu, Visible = true };
   tray.DoubleClick += (_,_) => Reveal();
  };
 }
 IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) {
  if (msg == 0x001A && themeMode == "system") ApplyTheme();
  if (msg == 0x031D) { attempts = 0; Capture(); }
  if (msg == 0x0312 && w.ToInt32() == hotkeyId) { Reveal(); handled = true; }
  return IntPtr.Zero;
 }
 void PositionPanel() {
  var area = SystemParameters.WorkArea;
  Width = Math.Max(1, area.Width);
  MinWidth = Math.Min(600, Width);
  Left = area.Left + (area.Width - Width) / 2;
  Top = area.Bottom - Height;
 }
 void Reveal() { RememberPasteTarget(); History.SelectedIndex = -1; PositionPanel(); Show(); WindowState = WindowState.Normal; Activate(); Search.Focus(); Search.SelectAll(); }
 void QueueAutoHide() {
  Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
   if (!IsVisible || exiting) return;
   // Popups, previews and settings belong to this process and must remain usable.
   GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
   if (pid != (uint)Environment.ProcessId) Dismiss();
  }));
 }
 void Dismiss() { previewWindow?.Close(); Hide(); }
 void HideClick(object sender, RoutedEventArgs e) => Dismiss();
 void AttachCardMenu(object sender, RoutedEventArgs e) {
  var card = (ListBoxItem)sender;
  if (card.ContextMenu != null) return;
  var menu = new ContextMenu();
  menu.SetResourceReference(Control.BackgroundProperty, "Card"); menu.SetResourceReference(Control.ForegroundProperty, "Text");
  void Add(string label, RoutedEventHandler action) {
   var item = new MenuItem { Header = label };
   item.Click += (s, args) => { card.IsSelected = true; action(s, args); };
   menu.Items.Add(item);
  }
  Add("复制", CopyClick);
  Add("查看完整内容    Space", PreviewClick);
  Add("收藏 / 取消收藏", FavoriteClick);
  menu.Items.Add(new Separator());
  Add("删除", DeleteClick);
  card.ContextMenu = menu;
 }
 void ScrollCards(object sender, MouseWheelEventArgs e) {
  if (History.Items.Count == 0) return;
  History.SelectedIndex = Math.Clamp(History.SelectedIndex + (e.Delta < 0 ? 1 : -1), 0, History.Items.Count - 1);
  History.ScrollIntoView(History.SelectedItem); e.Handled = true;
 }
 void SelectContextCard(object sender, MouseButtonEventArgs e) { ((ListBoxItem)sender).IsSelected = true; }
 void CardDoubleClick(object sender, MouseButtonEventArgs e) {
  if (e.ChangedButton != MouseButton.Left || sender is not ListBoxItem card || card.DataContext is not Clip item) return;
  e.Handled = true;
  QuickPaste(History.Items.IndexOf(item));
 }
 void PreviewClick(object sender, RoutedEventArgs e) => ShowPreview();
 void ShowPreview() {
  if ((History.SelectedItem ?? History.Items.Cast<Clip>().FirstOrDefault()) is not Clip item) return;
  previewWindow?.Close();
  var text = new TextBox { Text = item.DisplayText, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), Padding = new Thickness(24), FontSize = 14, Background = System.Windows.Media.Brushes.White };
  var window = new Window { Title = "内容预览 · Datto", Owner = this, Width = 640, Height = 420, MaxWidth = SystemParameters.WorkArea.Width, MaxHeight = SystemParameters.WorkArea.Height, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = false, Content = text, Background = System.Windows.Media.Brushes.White };
  if (item.IsImage) window.Content = new ScrollViewer { Content = new System.Windows.Controls.Image { Source = item.ReadImage(), Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(16) } };
  text.SetResourceReference(Control.BackgroundProperty, "Card"); text.SetResourceReference(Control.ForegroundProperty, "Text"); window.SetResourceReference(BackgroundProperty, "Card");
  window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape || (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None)) { window.Close(); e.Handled = true; } };
  window.Deactivated += (_,_) => QueueAutoHide();
  window.Closed += (_,_) => { previewWindow = null; History.Focus(); };
  previewWindow = window; window.Show();
 }
 void Capture() {
  if (SkipCapture()) { retry.Stop(); return; }
  try {
   var data = Clipboard.GetDataObject();
   if (data == null) return;
   var item = ClipFormats.Read(data);
   if (item == null) { last = ""; return; }
   var key = item.Fingerprint;
   if (key == last) return;
   last = key;
   var existing = clips.FirstOrDefault(x => x.Fingerprint == key);
   if (existing != null) { clips.Remove(existing); item.Favorite = existing.Favorite; item.Id = existing.Id; }
   clips.Insert(0, item);
   clips = clips.Where(x => x.Favorite).Concat(clips.Where(x => !x.Favorite).Take(preferences.HistoryLimit)).OrderByDescending(x => x.Created).ToList();
   Save(); Refresh();
  } catch (ExternalException) { if (++attempts <= 5) retry.Start(); else Status.Text = "剪贴板暂时忙，未能读取这条内容。"; }
  catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException or InvalidOperationException) { Status.Text = "未收录：" + e.Message; }
 }
 void Save() {
  try { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(clips)); File.Move(file + ".tmp", file, true); }
  catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status.Text = "保存失败：" + e.Message; }
 }
 void Refresh() {
  if (History == null) return;
  var id = (History.SelectedItem as Clip)?.Id;
  var query = Search.Text;
  var rows = clips.Where(x => (filter == "all" || (filter == "favorite" ? x.Favorite : filter == "link" ? x.IsLink : !x.IsLink)) && (x.DisplayText + " " + x.KindLabel).Contains(query, StringComparison.OrdinalIgnoreCase));
  var result = rows.OrderByDescending(x => x.Created).ToList();
  for (int i = 0; i < result.Count; i++) result[i].QuickLabel = i < 9 ? $"Ctrl {i + 1}" : "";
  History.ItemsSource = result;
  History.SelectedItem = result.FirstOrDefault(x => x.Id == id);
  UpdatePreview();
 }
 void UpdatePreview() { if (Empty != null) Empty.Visibility = History.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed; }
 void SearchChanged(object sender, TextChangedEventArgs e) => Refresh();
 void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();
 void FilterClick(object sender, RoutedEventArgs e) {
  filter = (string)((Button)sender).Tag;
  UpdateTabs();
  Refresh();
 }
 void CopyClick(object sender, RoutedEventArgs e) => Copy();
 void Copy() {
  if ((History.SelectedItem ?? History.Items.Cast<Clip>().FirstOrDefault()) is not Clip item) return;
  try { Clipboard.SetDataObject(ClipFormats.Write(item), true); last = item.Fingerprint; Status.Text = "已复制，切回目标应用即可粘贴。"; Dismiss(); }
  catch (ExternalException) { Status.Text = "剪贴板暂时忙，请再试一次。"; }
  catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException) { Status.Text = e.Message; }
 }
 void FavoriteClick(object sender, RoutedEventArgs e) { if (History.SelectedItem is Clip item) { item.Favorite = !item.Favorite; TrimHistory(); Save(); Refresh(); } }
 void DeleteClick(object sender, RoutedEventArgs e) { if (History.SelectedItem is Clip item) { clips.Remove(item); Save(); Refresh(); } }
 void OnKey(object sender, KeyEventArgs e) {
  if (Keyboard.Modifiers == ModifierKeys.Control && QuickIndex(e.Key) >= 0) { e.Handled = true; if (!e.IsRepeat) QuickPaste(QuickIndex(e.Key)); return; }
  if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control) { Search.Focus(); Search.SelectAll(); e.Handled = true; }
  else if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; }
  else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is not Button) { e.Handled = true; if (!e.IsRepeat) QuickPaste(History.SelectedIndex >= 0 ? History.SelectedIndex : 0); }
  else if (e.Key == Key.Space && !Search.IsKeyboardFocusWithin) { ShowPreview(); e.Handled = true; }
  else if (e.Key == Key.Down && Search.IsKeyboardFocusWithin) { if (History.SelectedIndex < 0 && History.Items.Count > 0) History.SelectedIndex = 0; History.Focus(); if (History.SelectedItem != null) ((ListBoxItem?)History.ItemContainerGenerator.ContainerFromItem(History.SelectedItem))?.Focus(); e.Handled = true; }
  else if ((e.Key == Key.Right || e.Key == Key.Left) && !Search.IsKeyboardFocusWithin) {
   History.SelectedIndex = Math.Clamp(History.SelectedIndex + (e.Key == Key.Right ? 1 : -1), 0, Math.Max(0, History.Items.Count-1));
   if (History.SelectedItem != null) History.ScrollIntoView(History.SelectedItem); e.Handled = true;
  }
 }
 public void VerifyInitialSelection() {
  if (!smokeMode) return;
  if (History.SelectedIndex != -1) throw new InvalidOperationException("Panel must open without a selected card");
  History.SelectedIndex = 0;
  Refresh();
  if (History.SelectedIndex != 0) throw new InvalidOperationException("Explicit selection must survive refresh");
  History.SelectedIndex = -1;
  Refresh();
  if (History.SelectedIndex != -1) throw new InvalidOperationException("Refresh must not select a card automatically");
 }
 public void ExitApplication() { settingsWindow?.Close(); exiting = true; Close(); Application.Current.Shutdown(); }
 protected override void OnClosing(CancelEventArgs e) { if (!exiting) { e.Cancel = true; Dismiss(); } base.OnClosing(e); }
 protected override void OnClosed(EventArgs e) { StopPaste(); retry.Stop(); RemoveClipboardFormatListener(handle); UnregisterHotKey(handle, hotkeyId); source?.RemoveHook(Hook); tray?.Icon?.Dispose(); tray?.Dispose(); base.OnClosed(e); }
}


















