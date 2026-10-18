// ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
/*
Derived from ImageGlass v9, Copyright (C) 2010 - 2026 DUONG DIEU PHAP.
ImageCrystal modifications, 2026. GPL-3.0-or-later; see LICENSE.
*/
using ImageGlass.Base.Photoing.Codecs;

namespace ImageGlass;

public partial class FrmMain
{
    // Direct file opens bypass the folder cache. Keep their owner alive while
    // displayed, then release it after the renderer has switched to the new image.
    private IgPhoto? _ownedDisplayedPhoto;

    private void SetOwnedDisplayedPhoto(IgPhoto? photo)
    {
        var previous = _ownedDisplayedPhoto;
        _ownedDisplayedPhoto = photo;
        if (!ReferenceEquals(previous, photo)) previous?.Dispose();
    }
}
