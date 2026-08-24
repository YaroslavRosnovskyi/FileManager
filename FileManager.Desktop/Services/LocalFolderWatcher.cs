using System.IO;

namespace FileManager.Desktop.Services;

public sealed class LocalFolderWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounce;
    private readonly Action<string> _log;

    public event EventHandler? FolderChanged;

    public LocalFolderWatcher(string path, Action<string>? log = null)
    {
        _log = log ?? (_ => { });

        _debounce = new System.Timers.Timer(400) { AutoReset = false };
        _debounce.Elapsed += (_, _) => FolderChanged?.Invoke(this, EventArgs.Empty);

        _watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Size,
            IncludeSubdirectories = false,
            InternalBufferSize = 64 * 1024
        };

        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;

        _watcher.EnableRaisingEvents = true;
        _log($"watching {path}");
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _log($"{e.ChangeType}: {e.Name}");
        Restart();
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _log($"Renamed: {e.OldName} -> {e.Name}");
        Restart();
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _log($"watcher error: {e.GetException().Message} - forcing full refresh");
        Restart();
    }

    private void Restart()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnChanged;
        _watcher.Deleted -= OnChanged;
        _watcher.Changed -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Error -= OnError;
        _watcher.Dispose();
        _debounce.Dispose();
    }
}