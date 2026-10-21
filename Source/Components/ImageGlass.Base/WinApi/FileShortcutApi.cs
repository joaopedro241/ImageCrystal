/*
ImageGlass Project - Image viewer for Windows
Copyright (C) 2010 - 2026 DUONG DIEU PHAP
Project homepage: https://imageglass.org

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
// ImageCrystal modifications by xenon, 2026-10-21; see NOTICE.ImageCrystal.txt.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ImageGlass.Base.WinApi;

public static class FileShortcutApi
{
    public enum ShortcutWindowStyle
    {
        Normal = 4,
        Maximized = 3,
        Minimized = 7,
    }


    /// <summary>
    /// Get the target path from shortcut (*.lnk)
    /// </summary>
    /// <param name="shortcutPath">Path of shortcut (*.lnk)</param>
    /// <returns></returns>
    public static string GetTargetPathFromShortcut(string shortcutPath)
    {
        IShellLinkW? link = null;

        try
        {
            link = (IShellLinkW)new ShellLinkCom();
            ((IPersistFile)link).Load(shortcutPath, 0);
            var path = new StringBuilder(32768);
            link.GetPath(path, path.Capacity, IntPtr.Zero, 4); // SLGP_RAWPATH: do not resolve/mutate targets.
            return path.ToString();
        }
        catch // (COMException)
        {
            // A COMException is thrown if the file is not a valid shortcut (.lnk) file 
            return "";
        }
        finally
        {
            if (link != null) Marshal.FinalReleaseComObject(link);
        }
    }


    /// <summary>
    /// Create shortcut file
    /// </summary>
    /// <param name="shortcutPath"></param>
    /// <param name="targetPath"></param>
    /// <param name="args"></param>
    /// <param name="windowStyle"></param>
    public static void CreateShortcut(string shortcutPath,
        string targetPath,
        string args = "",
        ShortcutWindowStyle windowStyle = ShortcutWindowStyle.Normal)
    {
        var shell = new IWshRuntimeLibrary.WshShell();

        try
        {
            var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);

            shortcut.TargetPath = targetPath;
            shortcut.IconLocation = targetPath;
            shortcut.Arguments = args;
            shortcut.WindowStyle = (int)windowStyle;
            shortcut.Save();
        }
        catch { }
    }
}
