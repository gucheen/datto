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
  var oversized = new DataObject(); oversized.SetText(new string('a',21*1024*1024));
  bool sizeRejected = false;
  try { ClipFormats.Read(oversized); } catch (InvalidDataException) { sizeRejected = true; }
  Check(sizeRejected, "Oversized capture");
  Console.WriteLine("FORMATS_OK: legacy history, rich text, PNG persistence, file/folder round trips, deduplication and invalid inputs");
 }
}
