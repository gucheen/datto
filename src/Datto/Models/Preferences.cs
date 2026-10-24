using System;
using System.Windows.Input;
namespace Datto;
public sealed class Preferences {
 public string[] ExcludedApps { get; set; } = Array.Empty<string>();
 public int HistoryLimit { get; set; } = 500;
 public uint Modifiers { get; set; } = 6;
 public uint Key { get; set; } = 0x56;
 public bool Valid => HistoryLimit >= 50 && HistoryLimit <= 10000 && Modifiers > 0 && Modifiers <= 7 && ((Key >= 0x30 && Key <= 0x5A) || (Key >= 0x70 && Key <= 0x7B));
 public string Shortcut => ((Modifiers & 2) != 0 ? "Ctrl + " : "") + ((Modifiers & 1) != 0 ? "Alt + " : "") + ((Modifiers & 4) != 0 ? "Shift + " : "") + KeyInterop.KeyFromVirtualKey((int)Key);
}
