using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace Datto;
public partial class MainWindow {
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] static extern uint SendInput(uint count, INPUT[] inputs, int size);
 [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION data; }
 [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
 [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort key, scan; public uint flags, time; public UIntPtr extra; }
 [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int x,y; public uint data,flags,time; public UIntPtr extra; }
 bool paused;
 Forms.ToolStripMenuItem? pauseMenu;
 IntPtr pasteTarget;
 DispatcherTimer? pasteTimer;
 public static string NormalizeProcess(string name) => name.Trim().EndsWith(".exe",StringComparison.OrdinalIgnoreCase) ? name.Trim()[..^4].ToLowerInvariant() : name.Trim().ToLowerInvariant();
 void PauseClick(object sender, RoutedEventArgs e) => ToggleListening();
 void ToggleListening() {
  paused = !paused; retry.Stop();
  PauseButton.Content = paused ? "▶" : "Ⅱ";
  PauseButton.ToolTip = paused ? "恢复监听" : "暂停监听";
  if (pauseMenu != null) pauseMenu.Text = paused ? "恢复监听" : "暂停监听";
  if (tray != null) tray.Text = paused ? "Datto · 监听已暂停" : "Datto · 剪贴板";
  Status.Text = paused ? "监听已暂停 · 已有历史仍可使用" : "监听已恢复 · Ctrl 1–9 快速粘贴";
 }
 bool SkipCapture() {
  if (paused || smokeMode) return true;
  var excluded = preferences.ExcludedApps ?? Array.Empty<string>();
  if (excluded.Length == 0) return false;
  var owner = GetClipboardOwner();
  if (owner == IntPtr.Zero) return true;
  GetWindowThreadProcessId(owner, out uint pid);
  try { using var process = Process.GetProcessById((int)pid); return excluded.Any(x => NormalizeProcess(x) == NormalizeProcess(process.ProcessName)); }
  catch (Exception e) when (e is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { return true; }
 }
 void RememberPasteTarget() {
  var target = GetForegroundWindow(); GetWindowThreadProcessId(target, out uint pid);
  if (pid != Environment.ProcessId) pasteTarget = target;
 }
 static int QuickIndex(Key key) => key >= Key.D1 && key <= Key.D9 ? key - Key.D1 : key >= Key.NumPad1 && key <= Key.NumPad9 ? key - Key.NumPad1 : -1;
 void QuickPaste(int index) {
  if (pasteTimer != null || index < 0 || index >= History.Items.Count) return;
  var target = pasteTarget;
  if (target == IntPtr.Zero || !IsWindow(target)) { Status.Text = "请从目标应用通过唤起快捷键打开面板，再快速粘贴。"; return; }
  if (History.Items[index] is not Clip item) return;
  try { Clipboard.SetDataObject(ClipFormats.Write(item), true); last = item.Fingerprint; }
  catch (ExternalException) { Status.Text = "剪贴板暂时忙，请重试。"; return; }
  catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException) { Status.Text = e.Message; return; }
  MarkUsed(item);
  Dismiss();
  if (!SetForegroundWindow(target)) { Reveal(); Status.Text = "无法激活目标窗口；内容已复制，请手动粘贴。"; return; }
  int ticks = 0;
  pasteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
  pasteTimer.Tick += (_,_) => {
   if (++ticks > 80 || !IsWindow(target) || GetForegroundWindow() != target) { StopPaste(); Status.Text = "自动粘贴已取消；内容已复制。"; return; }
   if (new[] { 0x01,0x0D,0x11,0x10,0x12,0x5B,0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) return;
   StopPaste();
   INPUT KeyInput(ushort key, bool up) => new() { type = 1, data = new INPUTUNION { keyboard = new KEYBDINPUT { key = key, flags = up ? 2u : 0u } } };
   var keys = new[] { KeyInput(0x11,false), KeyInput(0x56,false), KeyInput(0x56,true), KeyInput(0x11,true) };
   if (SendInput(4,keys,Marshal.SizeOf<INPUT>()) != 4) { Reveal(); Status.Text = "目标应用未接受模拟按键（可能以管理员权限运行）；请手动粘贴。"; }
  };
  pasteTimer.Start();
 }
 void StopPaste() { pasteTimer?.Stop(); pasteTimer = null; }
 void ClearHistory(bool keepFavorites, Window owner) {
  if (smokeMode) return;
  int count = clips.Count(x => !keepFavorites || !x.Favorite);
  if (count == 0) return;
  if (MessageBox.Show(owner, $"确定删除 {count} 条{(keepFavorites ? "普通" : "全部（包括收藏）")}记录？此操作无法撤销。系统剪贴板不受影响。", "清空历史", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
  var remaining = clips.Where(x => keepFavorites && x.Favorite).ToList();
  try {
   Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(remaining)); File.Move(file + ".tmp",file,true);
   retry.Stop(); clips = remaining; Refresh();
  } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show(owner,"清空失败，记录未修改：" + e.Message); }
 }
}



