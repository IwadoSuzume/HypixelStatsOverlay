using System.Windows;
using System.Windows.Input;
using HypixelStatsOverlay.Services;

namespace HypixelStatsOverlay;

/// <summary>
/// Styled replacement for the plain OS MessageBox previously used to ask "use this clipboard
/// string as your API key?" - same dark/red WitheredRose look as the rest of the app instead of
/// a bare Windows dialog.
/// </summary>
public partial class ApiKeyDetectedDialog : Window
{
    public bool Accepted { get; private set; }

    public ApiKeyDetectedDialog(string candidateKey)
    {
        InitializeComponent();
        TitleText.Text = Loc.T("Dialog_ApiKeyDetectedTitle");
        BodyText.Text = Loc.T("Dialog_ApiKeyDetectedBody");
        KeyText.Text = candidateKey;
        IgnoreButton.Content = Loc.T("Dialog_Ignore");
        UseKeyButton.Content = Loc.T("Dialog_UseKey");
    }

    /// <summary>Shows the dialog modally and returns whether the user chose to use the key.</summary>
    public static bool Show(Window owner, string candidateKey)
    {
        var dialog = new ApiKeyDetectedDialog(candidateKey) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Accepted;
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void UseKeyButton_Click(object sender, RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }

    private void IgnoreButton_Click(object sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close();
    }
}
