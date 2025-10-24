using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.IO;
using System.Text;
using FlightDeck_Installer.ViewModels;
using FlightDeck_Installer.Views;

namespace FlightDeck_Installer;

public partial class App : Application
{
    private static string logFileName = "FlightDeck-Installer.log";
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlightDeck", logFileName);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Configure NLog and clear the log file
        ConfigureLogging();

        // Redirect Console.WriteLine() to NLog
        Console.SetOut(new LoggerTextWriter(logger));

        logger.Info("Application initialized.");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
        logger.Info("Framework initialization completed.");
    }

    private void ConfigureLogging()
    {
        try
        {
            // Clear log file on startup
            if (File.Exists(logFilePath))
            {
                File.WriteAllText(logFilePath, string.Empty);
            }

            var config = new LoggingConfiguration();

            // Define a file target
            var fileTarget = new FileTarget("fileTarget")
            {
                FileName = logFilePath, // Logs next to executable
                Layout = "${longdate} [${level}] ${message}",
                KeepFileOpen = false,
                Encoding = Encoding.UTF8
            };

            config.AddTarget(fileTarget);
            config.AddRuleForAllLevels(fileTarget);

            // Apply configuration
            LogManager.Configuration = config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize logging: {ex.Message}");
        }
    }
}

// Custom TextWriter to capture Console.WriteLine() and send it to NLog
public class LoggerTextWriter : TextWriter
{
    private readonly Logger _logger;

    public LoggerTextWriter(Logger logger)
    {
        _logger = logger;
    }

    public override void WriteLine(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _logger.Info(value); // Redirect Console.WriteLine() to NLog
        }
    }

    public override Encoding Encoding => Encoding.UTF8;
}