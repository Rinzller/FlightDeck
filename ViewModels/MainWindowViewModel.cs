using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using ReactiveUI;
using WindowsShortcutFactory;
using System.Reflection;
using System.Linq;
using FlightDeck_Installer.Views;

namespace FlightDeck_Installer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private static string launcherName = "FlightDeck";
    private static string installerName = "FlightDeck-Installer";
    private static string releaseInfoUrl = "https://api.github.com/repos/Rinzller/FlightDeck/releases/latest";
    private string jsonFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlightDeck", "config.json");

    // Single HttpClient to avoid socket exhaustion
    private static readonly HttpClient httpClient = new HttpClient();

    // Json data model
    public class JsonDataModel
    {
        // From config.json
        public string launcher_path { get; set; }
        public string vr_enabled { get; set; }
        public string install_path { get; set; }
        public int options_preset { get; set; }
        public string optimization_enabled { get; set; }
        public string launcher_enabled { get; set; }
        public List<Dictionary<string, string>> startup_apps { get; set; }
    }

    public MainWindowViewModel()
    {
        // Load the config.json file
        GetConfigJson();
    }


    // Reused in FlightDeck-Avalonia Project
    // Get dictionary from config file
    public void GetConfigJson()
    {
        try
        {
            // Create the config file if it doesn't exist
            if (!File.Exists(jsonFilePath))
            {
                File.Create(jsonFilePath);
            }

            // Getting JSON data from json file
            string json = File.ReadAllText(jsonFilePath);
            JsonDataModel data = JsonSerializer.Deserialize<JsonDataModel>(json);

            InstallLocation = data.launcher_path;
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., file not found, file access issues)
            Console.WriteLine($"Error loading state: {ex.Message}");
        }
    }

    // Save dictionary as config file
    public void SetConfigJson()
    {
        // Initialize an empty or default instance of JsonDataModel
        JsonDataModel data = new JsonDataModel();

        // Check if the file exists and has content
        if (File.Exists(jsonFilePath) && new FileInfo(jsonFilePath).Length > 0)
        {
            // Getting JSON data from json file
            string json = File.ReadAllText(jsonFilePath);
            try
            {
                // Attempt to deserialize the JSON into the data model
                data = JsonSerializer.Deserialize<JsonDataModel>(json);
            }
            catch (JsonException ex)
            {
                // Handle JSON deserialization errors (e.g., if the JSON is malformed)
                Console.WriteLine($"Error deserializing JSON: {ex.Message}");
                // You might choose to log the error and/or assign default values to 'data'
            }
        }
        else
        {
            Console.WriteLine("The file is empty or does not exist. Using default configuration.");
            // Initialize 'data' with default values if needed
        }

        data.launcher_path = InstallLocation;

         // Serialize the modified data back to a JSON string
        var options = new JsonSerializerOptions { WriteIndented = true }; // Format the JSON for readability
        string modifiedJson = JsonSerializer.Serialize(data, options);

        // Write the modified JSON string back to the file
        File.WriteAllText(jsonFilePath, modifiedJson);
    }

    // Initialize Build with data
    private string? _build = $"Build: {Assembly.GetExecutingAssembly()
                     .GetCustomAttributes<AssemblyMetadataAttribute>()
                     .FirstOrDefault(a => a.Key == "GitTag")?.Value ?? "<Unknown>"}";
    public string? Build
    {
        get => _build;
        set => this.RaiseAndSetIfChanged(ref _build, value);
    }

    private string? _message = $"Choose the location where {launcherName} should be installed.";
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
            if (this.RaiseAndSetIfChanged(ref _installLocation, value) != null)
            {
                //Set this in the config.json
                SetConfigJson();

                //Set Message
                SetMessage();
            }
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
                Title = "Select your DCS install location",
                SuggestedStartLocation = await provider.TryGetFolderFromPathAsync(@"C:\Program Files\"),
                AllowMultiple = false
            };
            var dialog = await provider.OpenFolderPickerAsync(options);

            // Set the textbox text to the user input
            // Find a better way to get the first element, this SUCKS
            InstallLocation = Path.Combine(dialog[0].Path.LocalPath, launcherName);
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., file not found, file access issues)
            Console.WriteLine($"Error setting install location: {ex.Message}");
        }
    }

    public void SetMessage()
    {
        try
        {
            // Set content of the action button
            if (!Directory.Exists(InstallLocation))
            {
                Action = "Install";
            }
            else
            {
                Action = "Update";
            }

            // Set message to user
            Message = $"Click either {Action} or Uninstall";
            // Unhide buttons
            ButtonsVisible = "True";
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., file not found, file access issues)
            Console.WriteLine($"Error setting install message: {ex.Message}");
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

    private string? _action = "Install";
    public string Action
    {
        get => _action;
        set
        {
            this.RaiseAndSetIfChanged(ref _action, value);
        }
    }

    private bool _isShortcutEnabled = true;
    public bool IsShortcutEnabled
    {
        get => _isShortcutEnabled;
        set => this.RaiseAndSetIfChanged(ref _isShortcutEnabled, value);
    }

    // All of this after this point is ChatGPT
    public async void InstallLauncher()
    {
        try
        {
            TextColor = "Gray";
            Message = $"{Action} In Progress... Do not close this window.";
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

            // Fetch release info from GitHub
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");
            var releaseInfoResponse = await httpClient.GetAsync(releaseInfoUrl);
            releaseInfoResponse.EnsureSuccessStatusCode();
            var releaseInfo = await releaseInfoResponse.Content.ReadAsStringAsync();
            var release = JsonDocument.Parse(releaseInfo);

            // Download FlightDeck.exe
            string launcherUrl = null;
            foreach (var asset in release.RootElement.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == $"{launcherName}.exe")
                {
                    launcherUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(launcherUrl))
            {
                throw new Exception($"{launcherName}.exe not found in the latest release.");
            }

            currentProgress = await DownloadFile(launcherUrl, Path.Combine(InstallLocation, $"{launcherName}.exe"), currentProgress, totalSteps);

            // Download new installer with a different name
            string installerUrl = null;
            foreach (var asset in release.RootElement.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == $"{installerName}.exe")
                {
                    installerUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(installerUrl))
            {
                throw new Exception($"{installerName}.exe not found in the latest release.");
            }

            string newInstallerPath = Path.Combine(InstallLocation, $"{installerName}.new.exe");
            currentProgress = await DownloadFile(installerUrl, newInstallerPath, currentProgress, totalSteps);

            // Create config.json in Local APPDATA
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), launcherName);
            CreateConfigFile(Path.Combine(localAppDataPath, "config.json"));
            currentProgress++;
            UpdateProgress(currentProgress / totalSteps);

            if (IsShortcutEnabled)
            {
                CreateShortcut(Path.Combine(InstallLocation, $"{launcherName}.exe"), launcherName);
                currentProgress++;
                UpdateProgress(currentProgress / totalSteps);
            }

            ProgressValue = "100";
            TextColor = "SpringGreen";

            for (int i = 3; i >= 1; i--)
            {
                Message = $"{Action} Successful! Launching {launcherName} in {i} seconds...";
                await Task.Delay(1000); // Waits for 1000 milliseconds (1 second)
            }

            // PowerShell command for renaming and deleting the installer
            string originalInstallerPath = Path.Combine(InstallLocation, $"{installerName}.exe");
            string powerShellCommand = $@"
                while (Get-Process -Name '{installerName}' -ErrorAction SilentlyContinue) {{
                    Start-Sleep -Seconds 1
                }}
                Remove-Item -Path '{originalInstallerPath}' -Force
                Rename-Item -Path '{newInstallerPath}' -NewName '{originalInstallerPath}' -Force
            ";

            // Run PowerShell command asynchronously
            RunPowerShellCommandAsync(powerShellCommand);

            // Launch FlightDeck.exe
            LaunchApplication(Path.Combine(InstallLocation, $"{launcherName}.exe"));

            // Close the current application
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            ProgressValue = "0";
            Message = $"Please ensure FlightDeck is not running and you are connected to the internet.";
            TextColor = "Red";
        }
    }

    private async Task<double> DownloadFile(string url, string filePath, double currentProgress, int totalSteps)
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
                UpdateProgress(currentProgress / totalSteps + downloadProgress / totalSteps);
            }
        });

        using (var stream = await response.Content.ReadAsStreamAsync())
        {
            // Overwrite existing file if it exists
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
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
            Description = $"Shortcut to {launcherName}"
        };

        shortcut.Save(shortcutLocation);
    }

    private void CreateConfigFile(string configFilePath)
    {
        string directory = Path.GetDirectoryName(configFilePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(configFilePath, "{}");
        }
    }

    private void UpdateProgress(double progress)
    {
        double percentage = progress * 100;
        ProgressValue = percentage.ToString("F0");
    }

    public async void UninstallLauncher()
    {
        try
        {
            TextColor = "Gray";
            Message = "Uninstalling... Do not close this window.";
            ProgressVisible = "True";
            ProgressValue = "0";

            int totalSteps = 3;  // Total steps increased to 3
            int currentStep = 0;

            if (IsShortcutEnabled)
            {
                DeleteShortcut(launcherName);
                currentStep++;
                UpdateProgress((double)currentStep / totalSteps);
            }

            // Run PowerShell commands to wait for the installer to exit and delete folders
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), launcherName);

            string powerShellCommand = $@"
                while (Get-Process -Name '{installerName}' -ErrorAction SilentlyContinue) {{
                    Start-Sleep -Seconds 1
                }}
                Remove-Item -Path '{InstallLocation}' -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item -Path '{localAppDataPath}' -Recurse -Force -ErrorAction SilentlyContinue
            ";

            // Run PowerShell command asynchronously
            RunPowerShellCommandAsync(powerShellCommand);

            // Update progress to 100% since the powershell script will handle the rest
            ProgressValue = "100";
            TextColor = "SpringGreen";

            for (int i = 3; i >= 1; i--)
            {
                Message = $"Uninstall Successful! Exiting in {i} seconds...";
                await Task.Delay(1000); // Waits for 1000 milliseconds (1 second)
            }

            // Close the current application after 
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            ProgressValue = "0";
            Message = $"Uninstall failed: {ex.Message}";
            TextColor = "Red";
        }
    }

    // More ChatGPT on this one...
    private void RunPowerShellCommandAsync(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();
        // Do not wait for exit
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

    private void LaunchApplication(string applicationPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = applicationPath,
            CreateNoWindow = false,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
}
