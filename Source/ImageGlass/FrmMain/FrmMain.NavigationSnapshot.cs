/*
Derived from ImageGlass v9, Copyright (C) 2010 - 2026 DUONG DIEU PHAP.
ImageCrystal modifications, 2026. GPL-3.0-or-later; see LICENSE.
*/
using ImageGlass.Settings;

namespace ImageGlass;

public partial class FrmMain
{
    private static bool IsMissingImageFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try { File.GetAttributes(path); return false; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
        // Permission and malformed-file errors retain their normal error handling.
    }

    private int ResolveExistingImageIndex(int index, int direction)
    {
        if (InvokeRequired)
            return (int)Invoke(() => ResolveExistingImageIndex(index, direction));

        while (index >= 0 && index < Local.Images.Length)
        {
            var path = Local.Images.GetFilePath(index);
            if (!IsMissingImageFile(path)) return index;

            Local.Images.Remove(index);
            Gallery.Items.RemoveAt(index);
            if (index < Local.CurrentIndex) Local.CurrentIndex--;
            Local.CurrentIndex = Math.Min(Local.CurrentIndex, Local.Images.Length - 1);
            if (path == Local.InitialInputPath)
                Local.InitialInputPath = Path.GetDirectoryName(path) ?? string.Empty;
            LoadImageInfo();

            if (Local.Images.Length == 0)
            {
                Local.CurrentIndex = -1;
                Local.Metadata = null;
                PicMain.SetImage(null);
                LoadImageInfo();
                return -1;
            }
            if (direction < 0) index--;
            if (index < 0 || index >= Local.Images.Length)
            {
                if (!Config.EnableLoopBackNavigation) return -1;
                index = direction < 0 ? Local.Images.Length - 1 : 0;
            }
        }
        return -1;
    }
}
