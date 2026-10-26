using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
namespace Datto;

// Snapshot the icon with the content so history survives an app exit or uninstall.
static class ClipboardSource {
 static readonly Dictionary<string, (string Name, byte[]? Icon)> cache = new(StringComparer.OrdinalIgnoreCase);
 public static void Apply(Clip clip, uint processId) {
  if (processId == 0) return;
  try {
   using var process = Process.GetProcessById(checked((int)processId));
   clip.SourceApp = process.ProcessName;
   var path = process.MainModule?.FileName;
   if (string.IsNullOrEmpty(path)) return;
   if (!cache.TryGetValue(path, out var source)) {
    var name = FileVersionInfo.GetVersionInfo(path).FileDescription;
    source = (string.IsNullOrWhiteSpace(name) ? clip.SourceApp : name, ReadIcon(path));
    if (cache.Count >= 64) cache.Clear();
    cache[path] = source;
   }
   clip.SourceApp = source.Name;
   clip.SourceIconPng = source.Icon;
  } catch (Exception e) when (e is ArgumentException or Win32Exception or InvalidOperationException or NotSupportedException or IOException or UnauthorizedAccessException or OverflowException) {
   // Source metadata is optional; inaccessible/exited processes must not lose content.
  }
 }
 static byte[]? ReadIcon(string path) {
  using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
  if (icon == null) return null;
  var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
  bitmap.Freeze();
  var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
  using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
 }
}
