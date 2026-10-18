using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace HypixelStatsOverlay;

public partial class App : Application
{
    // Only one process should ever be alive at once, including while a prior instance is sitting
    // hidden in the tray - so a second launch (double-click, shortcut, etc.) signals that instance
    // to come back to the foreground instead of spawning a duplicate.
    private const string MutexName = "WitheredRose_SingleInstance_Mutex";
    private const string ShowEventName = "WitheredRose_ShowRequest_Event";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showEvent;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, MutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existingShowEvent))
            {
                using (existingShowEvent)
                    existingShowEvent.Set();
            }

            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"An unhandled error occurred:\n{args.Exception.Message}", "WitheredRose",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // MainWindow's constructor (config load, initial stat card fetch, log watcher setup, etc.)
        // runs while the splash animation plays, so the two overlap instead of adding up.
        var splash = new SplashWindow();
        splash.Show();
        var main = new MainWindow();
        await splash.PlayAsync();
        await splash.FlyLogoIntoAsync(main, main.HeaderLogoImage);
        splash.Close();
        main.Activate();

        // Deferred until the whole splash-to-main handoff has actually finished (not tied to
        // main's own Loaded event) - see the comment in MainWindow's constructor for why.
        main.CheckClipboardForApiKey();

        var dispatcher = Dispatcher;
        new Thread(() =>
        {
            while (_showEvent.WaitOne())
            {
                dispatcher.Invoke(() =>
                {
                    main.Show();
                    main.WindowState = WindowState.Normal;
                    main.Activate();
                });
            }
        }) { IsBackground = true }.Start();
    }
}
