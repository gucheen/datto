using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Datto;
public static class FormatChecks {
 public static void Run() {
  void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
  var old = JsonSerializer.Deserialize<Clip>("{\"Text\":\"old history\",\"Favorite\":true}")!;
  Check(old.HasContent && old.Favorite && old.KindLabel == "文本", "Legacy history");
  Check(old.SourceIcon == null && old.SourceLabel == "来源应用未知", "Legacy source fallback");
  var sourced = new Clip { Text = "source metadata" };
  ClipboardSource.Apply(sourced, (uint)Environment.ProcessId);
  Check(!string.IsNullOrWhiteSpace(sourced.SourceApp) && sourced.SourceIcon is { PixelWidth: 32 }, "Capture process name and executable icon");
  var sourcedCopy = JsonSerializer.Deserialize<Clip>(JsonSerializer.Serialize(sourced))!;
  Check(sourcedCopy.SourceApp == sourced.SourceApp && sourcedCopy.SourceIcon is { PixelWidth: 32 }, "Source metadata survives history reload");
  Check(sourcedCopy.Fingerprint == new Clip { Text = sourced.Text }.Fingerprint, "Source metadata must not change content deduplication");
  ClipboardSource.Apply(sourcedCopy, 0);
  Check(sourcedCopy.SourceApp == sourced.SourceApp, "Unknown process is safe");
  Check(new Clip { SourceIconPng = new byte[] { 1, 2, 3 } }.SourceIcon == null, "Invalid source icon fallback");
  var rich = new Clip { Text = "hello", Html = "<b>hello</b>", Rtf = @"{\rtf1\ansi hello}" };
  var richCopy = ClipFormats.Read(ClipFormats.Write(rich))!;
  Check(richCopy.Html == rich.Html && richCopy.Rtf == rich.Rtf && richCopy.Text == rich.Text, "Rich text round trip");
  Check(new Clip { Text = "hello" }.Fingerprint != rich.Fingerprint, "Different formats must not deduplicate");
  var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0,0,255,255, 0,255,0,255 }, 8);
  var imageData = new DataObject(); imageData.SetImage(bitmap);
  var image = ClipFormats.Read(imageData)!;
  var restored = JsonSerializer.Deserialize<Clip>(JsonSerializer.Serialize(image))!;
  Check(restored.IsImage && restored.ReadImage()!.PixelWidth == 2, "Image persistence");
  Check(ClipFormats.Read(ClipFormats.Write(restored))!.Fingerprint == image.Fingerprint, "Image round trip");
  imageData.SetData(DataFormats.Html, "<img src=\"https://example.com/image.png\">");
  imageData.SetData(DataFormats.Rtf, @"{\rtf1\ansi image}");
  imageData.SetText("image description");
  var mixedImage = ClipFormats.Read(imageData)!;
  Check(mixedImage.IsImage && !mixedImage.IsRich && mixedImage.KindLabel == "图片" && mixedImage.Thumbnail != null, "Image takes precedence over HTML/RTF/text");
  Check(mixedImage.Fingerprint == image.Fingerprint, "Image metadata must not change deduplication");
  Check(ClipFormats.Read(ClipFormats.Write(mixedImage))!.IsImage, "Mixed-format image pastes as an image");
  var files = new Clip { Files = new[] { AppContext.BaseDirectory, typeof(Clip).Assembly.Location } };
  Check(ClipFormats.Read(ClipFormats.Write(files))!.Files.Length == 2, "File and folder round trip");
  bool missingRejected = false;
  try { ClipFormats.Write(new Clip { Files = new[] { Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString()) } }); } catch (InvalidDataException) { missingRejected = true; }
  Check(missingRejected, "Missing files must be rejected");
  Check(ClipFormats.Read(new DataObject()) == null, "Empty clipboard");
  foreach (var marker in new[] { "Clipboard Viewer Ignore", "ExcludeClipboardContentFromMonitorProcessing" }) {
   foreach (var payload in new[] { new DataObject(DataFormats.UnicodeText, "temporary selection"), ClipFormats.Write(image), ClipFormats.Write(files) }) {
    payload.SetData(marker, new MemoryStream(new byte[] { 0 }), false);
    Check(ClipFormats.Read(payload) == null, marker + " excludes all content types regardless of marker value");
   }
  }
  foreach (var value in new object[] { 0, 0u, new byte[4], new MemoryStream(new byte[4]) }) {
   var ignored = new DataObject(DataFormats.UnicodeText, "private text");
   ignored.SetData("CanIncludeInClipboardHistory", value, false);
   Check(ClipFormats.Read(ignored) == null, "Zero history DWORD excludes capture: " + value.GetType().Name);
  }
  foreach (var value in new object[] { 1, 1u, new byte[] { 1, 0, 0, 0 }, new MemoryStream(new byte[] { 1, 0, 0, 0 }), new byte[3], "0" }) {
   var allowed = new DataObject(DataFormats.UnicodeText, "normal text");
   allowed.SetData("CanIncludeInClipboardHistory", value, false);
   Check(ClipFormats.Read(allowed)?.Text == "normal text", "Nonzero or malformed history flag does not exclude capture");
  }
  var positionedStream = new MemoryStream(new byte[4]); positionedStream.Position = 2;
  var positionedData = new DataObject(DataFormats.UnicodeText, "private text");
  positionedData.SetData("CanIncludeInClipboardHistory", positionedStream, false);
  Check(ClipFormats.Read(positionedData) == null && positionedStream.Position == 2, "History DWORD read preserves stream position");
  var cloudOnly = new DataObject(DataFormats.UnicodeText, "local history allowed");
  cloudOnly.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]), false);
  Check(ClipFormats.Read(cloudOnly)?.Text == "local history allowed", "Cloud-only opt-out allows local history");
  var oversized = new DataObject(); oversized.SetText(new string('a',21*1024*1024));
  bool sizeRejected = false;
  try { ClipFormats.Read(oversized); } catch (InvalidDataException) { sizeRejected = true; }
  Check(sizeRejected, "Oversized capture");
  Console.WriteLine("FORMATS_OK: legacy history, source icons, rich text, PNG persistence, file/folder round trips, deduplication, ignore markers and invalid inputs");
 }
}
