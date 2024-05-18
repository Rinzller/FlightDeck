using Avalonia.Controls;

namespace FlightDeck_Installer.Views;

public partial class MainWindow : Window
{
    public static MainWindow Instance;
    public MainWindow()
    {
        Instance = this;
        InitializeComponent();
    }
}