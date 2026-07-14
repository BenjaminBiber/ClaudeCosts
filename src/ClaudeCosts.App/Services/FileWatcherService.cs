using System.IO;
using System.Windows.Threading;
using ClaudeCosts.Core.Discovery;

namespace ClaudeCosts.App.Services;

/// <summary>
/// Watches every Claude <c>projects</c> root for <c>*.jsonl</c> changes and
/// raises <see cref="Changed"/> on the UI thread, debounced against bursts.
/// Must be constructed on the UI thread.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly DispatcherTimer _debounce;
    private readonly Dispatcher _dispatcher;

    public event EventHandler? Changed;

    public FileWatcherService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _debounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(1500),
        };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    public void Start()
    {
        foreach (var root in ClaudeDataLocator.GetProjectRoots())
        {
            try
            {
                var watcher = new FileSystemWatcher(root, "*.jsonl")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size |
                                   NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                };
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.Deleted += OnChanged;
                watcher.Renamed += OnChanged;
                watcher.Error += OnError;
                _watchers.Add(watcher);
            }
            catch
            {
                // a root became unwatchable — skip it
            }
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Kick();

    private void OnError(object sender, ErrorEventArgs e) => Kick();

    private void Kick() => _dispatcher.BeginInvoke(() =>
    {
        _debounce.Stop();
        _debounce.Start();
    });

    public void Dispose()
    {
        _debounce.Stop();
        foreach (var w in _watchers)
        {
            try
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            catch
            {
                // ignore
            }
        }
        _watchers.Clear();
    }
}
