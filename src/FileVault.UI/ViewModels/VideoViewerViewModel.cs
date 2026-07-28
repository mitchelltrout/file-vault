using CommunityToolkit.Mvvm.ComponentModel;
using FileVault.UI.Ipc;
using FileVault.UI.Models;
using FileVault.UI.Services;
using LibVLCSharp.Shared;

namespace FileVault.UI.ViewModels;

public partial class VideoViewerViewModel : ObservableObject, IDisposable
{
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private VaultMediaInput? _input;
    private Media? _media;

    public MediaPlayer? Player => _player;

    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private long _positionMs;
    [ObservableProperty] private long _durationMs;
    [ObservableProperty] private int _volume = 80;

    public void Open(IServiceClient client, string vaultPath, FileItemModel file)
    {
        Dispose();

        Core.Initialize();
        _libVlc = new LibVLC(enableDebugLogs: false, "--no-video-title-show");
        _player = new MediaPlayer(_libVlc) { Volume = Volume };
        _input = new VaultMediaInput(client, vaultPath, file.VaultPath, file.PlaintextLength);
        _media = new Media(_libVlc, _input);

        _player.Playing += (_, _) => IsPlaying = true;
        _player.Paused += (_, _) => IsPlaying = false;
        _player.Stopped += (_, _) => IsPlaying = false;
        _player.TimeChanged += (_, e) => PositionMs = e.Time;
        _player.LengthChanged += (_, e) => DurationMs = e.Length;
        _player.EndReached += (_, _) =>
        {
            // Must dispatch replay from a thread pool thread — LibVLC
            // deadlocks if you call Play from the EndReached callback.
            Task.Run(() => _player?.Play(_media));
        };

        _player.Play(_media);
    }

    public void TogglePlay()
    {
        if (_player == null) return;
        if (_player.IsPlaying) _player.Pause(); else _player.Play();
    }

    public void Seek(long ms)
    {
        if (_player != null) _player.Time = ms;
    }

    partial void OnVolumeChanged(int value)
    {
        if (_player != null) _player.Volume = value;
    }

    public void Dispose()
    {
        try { _player?.Stop(); } catch { }
        _media?.Dispose(); _media = null;
        _input?.Dispose(); _input = null;
        _player?.Dispose(); _player = null;
        _libVlc?.Dispose(); _libVlc = null;
    }
}
