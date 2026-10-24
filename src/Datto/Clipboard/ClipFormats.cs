using System;
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
 public static Clip? Read(IDataObject data) {
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
