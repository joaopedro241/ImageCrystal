// ImageCrystal, copyright (C) 2026 xenon. SPDX-License-Identifier: GPL-3.0-or-later
using ImageGlass.Base;
using ImageGlass.Base.Services;
using ImageGlass.Settings;
using System.Diagnostics;
using System.IO;

namespace ImageGlass;

public partial class FrmMain
{
    private PhotoSessionEndpoint? _photoSessionEndpoint;
    private readonly CancellationTokenSource _photoSessionSaveCancellation = new();
    private string? _sessionPhotoPath;
    private bool _savingPhotoSession;
    private int _photoSessionsDisposed;

    private void InitializePhotoSessions()
    {
        _photoSessionEndpoint = new();
        Disposed += (_, _) =>
        {
            if (Interlocked.Exchange(ref _photoSessionsDisposed, 1) != 0) return;
            _photoSessionSaveCancellation.Cancel();
            _photoSessionEndpoint.Dispose();
            // Save handlers retain the token, not the source, after disposal.
            _photoSessionSaveCancellation.Dispose();
        };
    }

    private void SetSessionPhotoPath(string? path)
    {
        _sessionPhotoPath = path;
        _photoSessionEndpoint?.Update(path);
    }

    private void RenameSessionPhotoPath(string oldPath, string newPath)
    {
        if (string.Equals(_sessionPhotoPath, oldPath, StringComparison.OrdinalIgnoreCase))
            SetSessionPhotoPath(newPath);
    }

    private async void MnuSavePhotoSession_Click(object? sender, EventArgs e)
    {
        if (_savingPhotoSession) return;
        _savingPhotoSession = true;
        MnuSavePhotoSession.Enabled = false;
        var token = _photoSessionSaveCancellation.Token;
        var lang = Config.Language;
        var key = $"{Name}.{nameof(MnuSavePhotoSession)}";
        try
        {
            if (_photoSessionEndpoint?.Error != null)
                throw new IOException(_photoSessionEndpoint.Error);
            var capture = await PhotoSessionEndpoint.CaptureAsync(
                PhotoSessionEndpoint.GetViewerProcessIds(), token);
            token.ThrowIfCancellationRequested();
            if (capture.Paths.Length == 0)
            {
                Config.ShowInfo(this, lang[$"{key}._Empty"], lang[key],
                    details: string.Join(Environment.NewLine, capture.Warnings));
                return;
            }
            var saved = await PhotoSessionWriter.SaveAsync(capture, cancellationToken: token);
            if (IsDisposed || token.IsCancellationRequested) return;
            var partial = saved.MissingCount + saved.ShortcutErrorCount + saved.CaptureWarningCount > 0;
            using var popup = new Popup
            {
                Title = lang[key],
                Heading = string.Format(lang[$"{key}._Saved"], saved.ShortcutCount, saved.PathCount),
                Description = saved.Directory,
                ShowTextInput = false,
                Note = partial ? string.Format(lang[$"{key}._Partial"], saved.MissingCount,
                    saved.ShortcutErrorCount, saved.CaptureWarningCount) : "",
                NoteStatusType = partial ? ColorStatusType.Warning : ColorStatusType.Neutral,
                ShowAcceptButton = true, AcceptButtonText = lang[$"{key}._OpenFolder"],
                ShowCancelButton = true, CancelButtonText = lang["_._Close"],
                TopMost = TopMost,
            };
            if ((PopupExitResult)popup.ShowDialog(this) == PopupExitResult.OK)
            {
                using var explorer = Process.Start(new ProcessStartInfo(saved.Directory) { UseShellExecute = true });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!IsDisposed) Config.ShowError(this, ex.Message, lang[key]);
        }
        finally
        {
            _savingPhotoSession = false;
            if (!IsDisposed) MnuSavePhotoSession.Enabled = true;
        }
    }
}
