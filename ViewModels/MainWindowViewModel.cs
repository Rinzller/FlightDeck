﻿using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ReactiveUI;
using WindowsShortcutFactory;
using System.Reflection;
using System.Linq;
using FlightDeck_Installer.Views;

namespace FlightDeck_Installer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    // Initialize Build with data
    private string? _build = $"Build: {Assembly.GetExecutingAssembly()
                     .GetCustomAttributes<AssemblyMetadataAttribute>()
                     .FirstOrDefault(a => a.Key == "GitTag")?.Value ?? "<Unknown>"}";
    public string? Build
    {
        get => _build;
        set => this.RaiseAndSetIfChanged(ref _build, value);
    }

    private string? _message = "Choose the location where you want FlightDeck to be installed.";
    public string Message
    {
        get => _message;
        set
        {
            this.RaiseAndSetIfChanged(ref _message, value);
        }
    }

    private string? _textColor = "Gray";
    public string TextColor
    {
        get => _textColor;
        set
        {
            this.RaiseAndSetIfChanged(ref _textColor, value);
        }
    }

    private string? _installLocation;
    public string InstallLocation
    {
        get => _installLocation;
        set
        {
            this.RaiseAndSetIfChanged(ref _installLocation, value);
        }
    }

    public async void SetInstallLocation()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(MainWindow.Instance);
            var provider = topLevel.StorageProvider;
            var options = new FolderPickerOpenOptions
            {
                Title="Select your DCS install location",
                SuggestedStartLocation = await provider.TryGetFolderFromPathAsync(@"C:\Program Files\"),
                AllowMultiple=false
            };
            var dialog = await provider.OpenFolderPickerAsync(options);

            // Set the textbox text to the user input
            // Find a better way to get the first element, this SUCKS
            InstallLocation = @$"{dialog[0].Path.LocalPath}\FlightDeck\";

            // Set message to user
            Message = "Click either Install or Uninstall!";
            // Unhide buttons
            ButtonsVisible = "True";
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., file not found, file access issues)
            Console.WriteLine($"Error setting install location: {ex.Message}");
        }
    }

    private string? _buttonsVisible = "False";
    public string ButtonsVisible
    {
        get => _buttonsVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _buttonsVisible, value);
        }
    }

    private string? _progressVisible = "False";
    public string ProgressVisible
    {
        get => _progressVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _progressVisible, value);
        }
    }

    private string? _progressValue;
    public string ProgressValue
    {
        get => _progressValue;
        set
        {
            this.RaiseAndSetIfChanged(ref _progressValue, value);
        }
    }

    private string? _shortcutEnabledContent = "X";
    public string ShortcutEnabledContent
    {
        get => _shortcutEnabledContent;
        set
        {
            this.RaiseAndSetIfChanged(ref _shortcutEnabledContent, value);
        }
    }

    private string? _shortcutEnabledFlag = "True";
    public string ShortcutEnabledFlag
    {
        get => _shortcutEnabledFlag;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _shortcutEnabledFlag, value) != null)
            {
                switch (ShortcutEnabledFlag)
                {
                    case "True":
                        ShortcutEnabledContent = "X";
                        break;
                    default:
                        ShortcutEnabledContent = "  ";
                        break;
                }
            }
        }
    }

    // All of this after this point is ChatGPT
    public async void InstallLauncher()
    {
        try
        {
            Message = "Do not close this window until the install/uninstall has completed.";
            ProgressVisible = "True";
            ProgressValue = "0";

            int totalSteps = 4;  // Total steps increased to 4
            double currentProgress = 0;

            if (!Directory.Exists(InstallLocation))
            {
                Directory.CreateDirectory(InstallLocation);
            }
            currentProgress++;
            UpdateProgress(currentProgress / totalSteps);

            var httpClient = new HttpClient();

            // Fetch release info from GitHub
            var releaseInfoUrl = "https://api.github.com/repos/Rinzller/FlightDeck/releases/latest";
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");
            var releaseInfoResponse = await httpClient.GetAsync(releaseInfoUrl);
            releaseInfoResponse.EnsureSuccessStatusCode();
            var releaseInfo = await releaseInfoResponse.Content.ReadAsStringAsync();
            var release = JsonDocument.Parse(releaseInfo);

            // Download FlightDeck.exe
            string flightDeckExeUrl = null;
            foreach (var asset in release.RootElement.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == "FlightDeck.exe")
                {
                    flightDeckExeUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(flightDeckExeUrl))
            {
                throw new Exception("FlightDeck.exe not found in the latest release.");
            }

            currentProgress = await DownloadFile(httpClient, flightDeckExeUrl, Path.Combine(InstallLocation, "FlightDeck.exe"), currentProgress, totalSteps);

            // Download FlightDeck-Installer.exe
            string flightDeckInstallerUrl = null;
            foreach (var asset in release.RootElement.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == "FlightDeck-Installer.exe")
                {
                    flightDeckInstallerUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(flightDeckInstallerUrl))
            {
                throw new Exception("FlightDeck-Installer.exe not found in the latest release.");
            }

            currentProgress = await DownloadFile(httpClient, flightDeckInstallerUrl, Path.Combine(InstallLocation, "FlightDeck-Installer.exe"), currentProgress, totalSteps);

            if (ShortcutEnabledFlag == "True")
            {
                CreateShortcut(Path.Combine(InstallLocation, "FlightDeck.exe"), "FlightDeck");
                currentProgress++;
                UpdateProgress(currentProgress / totalSteps);
            }

            ProgressValue = "100";
            Message = "Install Successful! You can close this window.";
            TextColor = "SpringGreen";
        }
        catch (Exception ex)
        {
            ProgressValue = "0";
            Message = ex.Message;
            TextColor = "Red";
        }
    }

    private async Task<double> DownloadFile(HttpClient httpClient, string url, string filePath, double currentProgress, int totalSteps)
    {
        var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var canReportProgress = totalBytes != -1;

        long bytesDownloaded = 0;
        var progress = new Progress<long>(bytes =>
        {
            bytesDownloaded += bytes;
            if (canReportProgress)
            {
                double downloadProgress = (double)bytesDownloaded / totalBytes;
                UpdateProgress((currentProgress + downloadProgress) / totalSteps);
            }
        });

        using (var stream = await response.Content.ReadAsStreamAsync())
        {
            using (var fileStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true))
            {
                await CopyToAsync(stream, fileStream, progress);
            }
        }

        currentProgress++;
        UpdateProgress(currentProgress / totalSteps);
        return currentProgress;
    }

    private async Task CopyToAsync(Stream source, Stream destination, IProgress<long> progress = null, int bufferSize = 81920)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (bufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "Buffer size must be greater than zero.");
        }

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;
            progress?.Report(bytesRead);
        }
    }

    private void CreateShortcut(string targetPath, string shortcutName)
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string shortcutLocation = Path.Combine(desktopPath, $"{shortcutName}.lnk");

        var shortcut = new WindowsShortcut
        {
            Path = targetPath,
            Description = "Shortcut to FlightDeck"
        };

        shortcut.Save(shortcutLocation);
    }

    private void UpdateProgress(double progress)
    {
        double percentage = progress * 100;
        ProgressValue = percentage.ToString("F0");
    }

    public void UninstallLauncher()
    {
        try
        {
            ProgressVisible = "True";
            ProgressValue = "0";
            Message = "Uninstalling... Do not close this window.";

            int totalSteps = 2;
            int currentStep = 0;

            if (ShortcutEnabledFlag == "True")
            {
                DeleteShortcut("FlightDeck");
                currentStep++;
                UpdateProgress((double)currentStep / totalSteps);
            }

            if (Directory.Exists(InstallLocation))
            {
                Directory.Delete(InstallLocation, true);
                currentStep++;
                UpdateProgress((double)currentStep / totalSteps);
                Console.WriteLine("Installation folder deleted successfully.");
            }
            else
            {
                Console.WriteLine("Installation folder not found.");
            }

            ProgressValue = "100";
            Message = "Uninstall Successful! You can close this window.";
            TextColor = "SpringGreen";
        }
        catch (Exception ex)
        {
            ProgressValue = "0";
            Message = $"Uninstall failed: {ex.Message}";
            TextColor = "Red";
        }
    }

    private void DeleteShortcut(string shortcutName)
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string shortcutLocation = Path.Combine(desktopPath, $"{shortcutName}.lnk");

        if (File.Exists(shortcutLocation))
        {
            File.Delete(shortcutLocation);
            Console.WriteLine("Shortcut deleted successfully.");
        }
        else
        {
            Console.WriteLine("Shortcut not found.");
        }
    }
}