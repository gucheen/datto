using System;
using System.Buffers.Binary;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
namespace Datto;
public static class ClipFormats {
 // Bound individual captures so a large clipboard cannot grow history without limit.
 const int MaxBytes = 20 * 1024 * 1024;
 public static bool ShouldIgnore(IDataObject data) {
  // Presence alone opts out, regardless of the marker's payload.
  if (data.GetDataPresent("Clipboard Viewer Ignore", false) ||
      data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing", false)) return true;
  const string historyFormat = "CanIncludeInClipboardHistory";
  if (!data.GetDataPresent(historyFormat, false)) return false;
  // Windows specifies a little-endian DWORD; WPF exposes native data as a stream.
  var value = data.GetData(historyFormat, false);
  if (value is uint unsigned) return unsigned == 0;
  if (value is int signed) return signed == 0;
  if (value is byte[] bytes) return bytes.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 0;
  if (value is Stream stream) {
   long position = stream.CanSeek ? stream.Position : 0;
   try {
    if (stream.CanSeek) stream.Position = 0;
    Span<byte> buffer = stackalloc byte[4];
    return stream.ReadAtLeast(buffer, 4, throwOnEndOfStream: false) == 4 && BinaryPrimitives.ReadUInt32LittleEndian(buffer) == 0;
   } finally {
    if (stream.CanSeek) stream.Position = position;
   }
  }
  return false;
 }
 public static Clip? Read(IDataObject data) {
  if (ShouldIgnore(data)) return null;
  var clip = new Clip();
  if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths) {
   clip.Files = paths.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
  } else {
   // Browsers and editors can supply HTML/RTF alongside the actual image.
   // Prefer image pixels so these copies get a thumbnail and paste as images.
   if (data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is BitmapSource bitmap) {
    if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 40_000_000) throw new InvalidDataException("图片超过 4000 万像素，未收录。");
    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = new MemoryStream(); encoder.Save(stream); clip.ImagePng = stream.ToArray(); clip.Text = "";
   } else {
    clip.Text = data.GetData(DataFormats.UnicodeText) as string ?? data.GetData(DataFormats.Text) as string ?? "";
    clip.Html = data.GetData(DataFormats.Html, false) as string;
    clip.Rtf = data.GetData(DataFormats.Rtf, false) as string;
   }
   if (clip.Text.Length == 0 && clip.Rtf != null) {
    try { var document = new FlowDocument(); var range = new TextRange(document.ContentStart, document.ContentEnd); using var stream = new MemoryStream(Encoding.UTF8.GetBytes(clip.Rtf)); range.Load(stream, DataFormats.Rtf); clip.Text = range.Text; }
    catch (ArgumentException) { }
   }
  }
  if ((long)Encoding.UTF8.GetByteCount(clip.Text) + Encoding.UTF8.GetByteCount(clip.Html ?? "") + Encoding.UTF8.GetByteCount(clip.Rtf ?? "") + (clip.ImagePng?.Length ?? 0) + clip.Files.Sum(x => (long)Encoding.UTF8.GetByteCount(x)) > MaxBytes) throw new InvalidDataException("单条内容超过 20 MB，未收录。");
  return clip.HasContent ? clip : null;
 }
 public static DataObject Write(Clip clip) {
  var data = new DataObject();
  if (clip.IsFiles) {
   if (clip.Files.Any(x => !File.Exists(x) && !Directory.Exists(x))) throw new InvalidDataException("原文件或文件夹已移动或删除，无法复制。");
   var paths = new StringCollection(); paths.AddRange(clip.Files); data.SetFileDropList(paths);
  } else if (clip.IsImage) {
   data.SetImage(clip.ReadImage() ?? throw new InvalidDataException("图片内容损坏，无法复制。"));
  } else {
   if (clip.Text.Length > 0) data.SetText(clip.Text);
   if (clip.Html != null) data.SetData(DataFormats.Html, clip.Html);
   if (clip.Rtf != null) data.SetData(DataFormats.Rtf, clip.Rtf);
  }
  return data;
 }
}
