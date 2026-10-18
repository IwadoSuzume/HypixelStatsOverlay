using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HypixelStatsOverlay;

/// <summary>Brief startup splash: logo fades/scales in, a progress bar fills alongside it, then
/// <see cref="PlayAsync"/> completes so the caller can swap in the real UI. Fixed-duration (not tied
/// to how long anything actually takes to load) - the caller constructs MainWindow (which kicks off
/// its own stat-card fetch/log-watcher setup) before awaiting this, so real data loading overlaps
/// the animation instead of waiting for it.</summary>
public partial class SplashWindow : Window
{
    private const double TotalSeconds = 10.0;

    public SplashWindow()
    {
        InitializeComponent();
        StartGlowBreathing();
    }

    /// <summary>Slow pulsing glow around the splash window's border - independent of PlayAsync's
    /// fixed-duration logo/progress animation, so it keeps breathing for as long as the splash is up.</summary>
    private void StartGlowBreathing()
    {
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var blur = new DoubleAnimation(16, 30, TimeSpan.FromSeconds(1.6))
        {
            EasingFunction = ease,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        var glow = new DoubleAnimation(0.45, 0.9, TimeSpan.FromSeconds(1.6))
        {
            EasingFunction = ease,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        GlowBlur.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, blur);
        GlowLayer.BeginAnimation(OpacityProperty, glow);
    }

    public Task PlayAsync()
    {
        var tcs = new TaskCompletionSource();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.4))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleUp = new DoubleAnimation(0.7, 1.0, TimeSpan.FromSeconds(0.4))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(scaleUp, 90);
        LogoImage.BeginAnimation(OpacityProperty, fadeIn);
        LogoScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleUp);
        LogoScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleUp);

        var fill = new DoubleAnimation(0, 205, TimeSpan.FromSeconds(TotalSeconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        fill.Completed += (_, _) => tcs.TrySetResult();
        ProgressFill.BeginAnimation(WidthProperty, fill);

        return tcs.Task;
    }

    /// <summary>Flies the splash logo across the screen into <paramref name="destinationLogo"/>'s spot on
    /// <paramref name="destinationWindow"/>, shrinking as it goes, while this splash dissolves to reveal the
    /// real window underneath. WPF has no shared-element transition between two independent top-level windows,
    /// so this fakes one: a borderless topmost window is spawned holding a copy of the logo image, sized/positioned
    /// to exactly overlap this splash's logo, then animated to the destination's screen rect. By the time it
    /// closes it's pixel-aligned with the real logo already sitting there, so the handoff reads as one continuous
    /// piece of art rather than two images swapping.</summary>
    public Task FlyLogoIntoAsync(Window destinationWindow, FrameworkElement destinationLogo)
    {
        var tcs = new TaskCompletionSource();

        var startRect = GetScreenRect(LogoImage);

        // Reveal the real window now - this splash is Topmost so it still renders above it while it dissolves.
        destinationWindow.Show();
        var targetRect = GetScreenRect(destinationLogo);

        var flightImage = new System.Windows.Controls.Image
        {
            Source = LogoImage.Source,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(flightImage, BitmapScalingMode.HighQuality);

        var flyover = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Left = startRect.Left,
            Top = startRect.Top,
            Width = startRect.Width,
            Height = startRect.Height,
            Content = flightImage
        };

        LogoImage.Opacity = 0;
        flyover.Show();

        const double seconds = 0.5;
        var duration = TimeSpan.FromSeconds(seconds);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        void Animate(DependencyProperty property, double from, double to)
            => flyover.BeginAnimation(property, new DoubleAnimation(from, to, duration) { EasingFunction = ease });

        Animate(Window.LeftProperty, startRect.Left, targetRect.Left);
        Animate(Window.TopProperty, startRect.Top, targetRect.Top);
        Animate(Window.HeightProperty, startRect.Height, targetRect.Height);

        var widthAnim = new DoubleAnimation(startRect.Width, targetRect.Width, duration) { EasingFunction = ease };
        widthAnim.Completed += (_, _) =>
        {
            flyover.Close();
            tcs.TrySetResult();
        };
        flyover.BeginAnimation(Window.WidthProperty, widthAnim);

        BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, duration) { EasingFunction = ease });

        return tcs.Task;
    }

    /// <summary>Screen-space bounds of <paramref name="element"/> in the DIPs that Window.Left/Top/Width/Height
    /// use - PointToScreen returns physical device pixels, which only match those DIPs 1:1 at 100% scaling, so
    /// this converts through the element's own CompositionTarget to stay correct at other DPI settings too.</summary>
    private static Rect GetScreenRect(FrameworkElement element)
    {
        var topLeftDevice = element.PointToScreen(new System.Windows.Point(0, 0));
        var source = PresentationSource.FromVisual(element);
        var topLeft = source is null
            ? topLeftDevice
            : source.CompositionTarget.TransformFromDevice.Transform(topLeftDevice);
        return new Rect(topLeft.X, topLeft.Y, element.ActualWidth, element.ActualHeight);
    }
}
