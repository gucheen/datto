using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
namespace Datto;
public class Clip {
 public Guid Id { get; set; } = Guid.NewGuid();
 public string Text { get; set; } = "";
 public bool Favorite { get; set; }
 public DateTime Created { get; set; } = DateTime.Now;
 public DateTime? LastUsed { get; set; }
 [JsonIgnore] public DateTime RecentActivity => LastUsed ?? Created;
 public bool IsLink => !IsImage && !IsFiles && !IsRich && Uri.TryCreate(Text.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http");
 public string HeaderColor => IsLink ? "#5D9DD5" : "#DAA34E";
 public string KindLabel => IsFiles ? "文件" : IsImage ? "图片" : IsRich ? "富文本" : IsLink ? "链接" : "文本";
 public string KindGlyph => IsFiles ? "▤" : IsImage ? "▧" : IsRich ? "¶" : IsLink ? "↗" : "Aa";
 public string TimeLabel => Created.ToString("MM-dd HH:mm");
 public string FooterLabel => IsFiles ? $"{Files.Length} 个文件 / 文件夹" : IsImage ? $"PNG · {ImagePng!.Length / 1024.0:N0} KB" : IsLink && Uri.TryCreate(Text.Trim(), UriKind.Absolute, out var uri) ? uri.Host : $"{Text.Length} 个字符";
 public string FavoriteMark => Favorite ? "★" : "";
 public string Title => Text.Split('\n')[0].TrimEnd('\r');
 public string Summary => Text.Replace('\r',' ').Replace('\n',' ');
 [System.Text.Json.Serialization.JsonIgnore] public string QuickLabel { get; set; } = "";
 public string Meta => $"{(Favorite ? "★  " : "")}{(IsLink ? "链接" : "文本")}  ·  {Created:MM-dd HH:mm}";

 public string? Html { get; set; }
 public string? Rtf { get; set; }
 public string[] Files { get; set; } = Array.Empty<string>();
 public byte[]? ImagePng { get; set; }
 [JsonIgnore] public bool IsImage => ImagePng is { Length: > 0 };
 [JsonIgnore] public bool IsFiles => Files.Length > 0;
 [JsonIgnore] public bool IsRich => !IsImage && !IsFiles && (Html != null || Rtf != null);
 [JsonIgnore] public bool HasContent => IsImage || IsFiles || IsRich || !string.IsNullOrEmpty(Text);
 [JsonIgnore] public string DisplayText => IsFiles ? string.Join("\n", Files) : IsImage ? "图片" : string.IsNullOrEmpty(Text) ? "富文本内容（粘贴到支持格式的应用查看）" : Text;
 [JsonIgnore] public string Fingerprint => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Text, Html, Rtf, Files, ImagePng })));
 [JsonIgnore] public BitmapSource? Thumbnail => ReadImage(320);
 public BitmapSource? ReadImage(int width = 0) {
  if (!IsImage) return null;
  try {
   using var stream = new MemoryStream(ImagePng!);
   var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream;
   if (width > 0) image.DecodePixelWidth = width;
   image.EndInit(); image.Freeze(); return image;
  } catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException or InvalidOperationException) { return null; }
 }
}
