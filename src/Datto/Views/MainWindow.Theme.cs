using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
namespace Datto;
public partial class MainWindow {
 string themeMode = "system";
 readonly string themeFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Datto", "theme.txt");
 Forms.ToolStripMenuItem? themeMenu;
 void InitializeTheme(bool smoke) {
  if (!smoke) { try { var saved = File.ReadAllText(themeFile).Trim(); if (saved is "system" or "dark" or "light") themeMode = saved; } catch (IOException) {} catch (UnauthorizedAccessException) {} }
  if (smoke) themeMode = Array.Exists(Environment.GetCommandLineArgs(), x => x == "--dark") ? "dark" : "light";
  ApplyTheme();
 }
 void ApplyTheme() {
  bool dark = themeMode == "dark";
  if (themeMode == "system") {
   try { dark = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
   catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException) { dark = false; }
  }
  void Set(string key, string light, string night) => Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
  Set("Panel", "#F2D4D5D9", "#F2181C24");
  Set("Card", "#FFFDFDFE", "#FF272D38");
  Set("Text", "#363639", "#F2F4F8");
  Set("Muted", "#666A74", "#CCD2DE");
  Set("Input", "#DDE2E2E6", "#FF363E4C");
  Set("Tab", "#DBDBDF", "#465166");
  Set("Hover", "#DDE1E8", "#3C475A");
  Set("HeaderText", "#FFFFFF", "#FFFFFF");
  Set("Accent", "#A17018", "#F2CC7B");
  Set("Edge", "#40FFFFFF", "#405E6574");
  Set("ScrollGrip", "#50707682", "#507F8DA3");
  Set("ScrollGripHover", "#90707682", "#9099A8BE");
  Set("ScrollGripActive", "#C0606978", "#C0ADBDD3");
  Set("TextHeader", "#DAA34E", "#624C2D");
  Set("LinkHeader", "#5D9DD5", "#294E70");
  UpdateTabs();
  if (themeMenu != null) foreach (Forms.ToolStripMenuItem item in themeMenu.DropDownItems) item.Checked = (string?)item.Tag == themeMode;
 }
 void UpdateTabs() {
  if (AllTab == null) return;
  AllTab.SetResourceReference(BackgroundProperty, filter == "all" ? "Tab" : "Clear");
  FavoriteTab.SetResourceReference(BackgroundProperty, filter == "favorite" ? "Tab" : "Clear");
  Application.Current.Resources["Clear"] = Brushes.Transparent;
 }
 void AddThemeMenu(Forms.ContextMenuStrip menu) {
  themeMenu = new Forms.ToolStripMenuItem("外观");
  foreach (var option in new[] { ("system", "跟随系统"), ("light", "浅色"), ("dark", "深色") }) {
   var item = new Forms.ToolStripMenuItem(option.Item2) { Tag = option.Item1, Checked = themeMode == option.Item1 };
   item.Click += (_,_) => {
    themeMode = option.Item1; ApplyTheme();
    try { Directory.CreateDirectory(Path.GetDirectoryName(themeFile)!); File.WriteAllText(themeFile, themeMode); }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status.Text = "外观已切换，但设置保存失败。"; }
   };
   themeMenu.DropDownItems.Add(item);
  }
  menu.Items.Add(themeMenu);
 }
}

