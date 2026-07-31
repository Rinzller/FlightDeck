using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ReactiveUI;
using WindowsShortcutFactory;
using System.Reflection;
using System.Linq;
using FlightDeck_Installer.Views;
using System.Security.Principal;

namespace FlightDeck_Installer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private static readonly string launcherName = "FlightDeck";
    private static readonly string installerName = "FlightDeck-Installer";
    private static readonly string releaseInfoUrl = "https://api.github.com/repos/Rinzller/FlightDeck/releases/latest";
    private static readonly string configFileName = "FlightDeck-Installer.json";
    private readonly string jsonFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlightDeck", configFileName);
    private static readonly string DefaultInstallParent =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

    // Single HttpClient to avoid socket exhaustion
    private static readonly HttpClient httpClient = new HttpClient();

    // Json data model
    public class JsonDataModel
    {
        // From config file
        public string launcher_path { get; set; } = string.Empty;
    }

    public MainWindowViewModel()
    {
        // Load the config file
        GetConfigJson();

        if (string.IsNullOrWhiteSpace(InstallLocation))
            InstallLocation = Path.Combine(DefaultInstallParent, launcherName);
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private bool CanWriteToInstallLocation()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(InstallLocation))
                return false;

            Directory.CreateDirectory(InstallLocation);
            var probe = Path.Combine(InstallLocation, $".flightdeck_write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private bool InstallLocationRequiresAdministrator() =>
        !string.IsNullOrWhiteSpace(InstallLocation) &&
        !IsRunningAsAdministrator() &&
        !CanWriteToInstallLocation();

    private bool RestartElevatedIfNeeded()
    {
        if (CanWriteToInstallLocation() || IsRunningAsAdministrator())
            return false;

        SetConfigJson();

        try
        {
            var exePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("Unable to locate installer executable.");

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            });

            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to restart elevated: {ex.Message}");
            Message = "This location requires administrator access. Please approve the elevation prompt or choose another location.";
            TextColor = "Red";
            return true;
        }
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
                var directory = Path.GetDirectoryName(jsonFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(jsonFilePath, JsonSerializer.Serialize(new JsonDataModel(), options));
            }

            // Getting JSON data from json file
            string json = File.ReadAllText(jsonFilePath);
            JsonDataModel? data = string.IsNullOrWhiteSpace(json)
                ? new JsonDataModel()
                : JsonSerializer.Deserialize<JsonDataModel>(json);

            // Check if data is null
            if (data != null)
            {
                InstallLocation = data.launcher_path;
            }
            else
            {
                Console.WriteLine("Deserialization returned null.");
            }
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
                data = JsonSerializer.Deserialize<JsonDataModel>(json) ?? new JsonDataModel();
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
    private string _build = $"Build: {Assembly.GetExecutingAssembly()
                     .GetCustomAttributes<AssemblyMetadataAttribute>()
                     .FirstOrDefault(a => a.Key == "GitTag")?.Value ?? "<Unknown>"}";
    public string Build
    {
        get => _build;
        set => this.RaiseAndSetIfChanged(ref _build, value);
    }

    private string _message = $"Choose the location where {launcherName} should be installed.";
    public string Message
    {
        get => _message;
        set
        {
            this.RaiseAndSetIfChanged(ref _message, value);
        }
    }

    private string _textColor = "Gray";
    public string TextColor
    {
        get => _textColor;
        set
        {
            this.RaiseAndSetIfChanged(ref _textColor, value);
        }
    }

    private string _adminWarningVisible = "False";
    public string AdminWarningVisible
    {
        get => _adminWarningVisible;
        set => this.RaiseAndSetIfChanged(ref _adminWarningVisible, value);
    }

    private string _adminWarningMessage = "This install location requires administrator approval.";
    public string AdminWarningMessage
    {
        get => _adminWarningMessage;
        set => this.RaiseAndSetIfChanged(ref _adminWarningMessage, value);
    }

    private string _installLocation = string.Empty;
    public string InstallLocation
    {
        get => _installLocation;
        set
        {
            if (_installLocation == value) return;

            this.RaiseAndSetIfChanged(ref _installLocation, value);

            if (!string.IsNullOrWhiteSpace(_installLocation))
            {
                //Set this in the config file
                SetConfigJson();

                //Set Message
                SetMessage();
                UpdateAdminWarning();
            }
        }
    }

    public async void SetInstallLocation()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(MainWindow.Instance);
            var provider = topLevel?.StorageProvider ?? throw new InvalidOperationException("No storage provider.");
            var options = new FolderPickerOpenOptions
            {
                Title = "Select the FlightDeck install parent folder",
                SuggestedStartLocation = await provider.TryGetFolderFromPathAsync(DefaultInstallParent),
                AllowMultiple = false
            };
            var dialog = await provider.OpenFolderPickerAsync(options);

            if (dialog.Any())
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
            if (string.IsNullOrWhiteSpace(InstallLocation))
                return;

            if (!Directory.Exists(InstallLocation))
            {
                Action = "Install";
            }
            else
            {
                Action = "Update";
            }

            var requiresAdministrator = InstallLocationRequiresAdministrator();
            if (requiresAdministrator)
            {
                Action = "Run as Admin";
            }

            // Set message to user
            Message = requiresAdministrator
                ? "Click Run as Admin to continue."
                : $"Click either {Action} or Uninstall";
            // Unhide buttons
            ButtonsVisible = "True";
            UpdateAdminWarning();
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., file not found, file access issues)
            Console.WriteLine($"Error setting install message: {ex.Message}");
        }
    }

    private void UpdateAdminWarning()
    {
        if (InstallLocationRequiresAdministrator())
        {
            AdminWarningMessage = "This install location requires elevated permissions.";
            AdminWarningVisible = "True";
            Action = "Run as Admin";
        }
        else
        {
            AdminWarningVisible = "False";
        }
    }

    private string _buttonsVisible = "False";
    public string ButtonsVisible
    {
        get => _buttonsVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _buttonsVisible, value);
        }
    }

    private string _progressVisible = "False";
    public string ProgressVisible
    {
        get => _progressVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _progressVisible, value);
        }
    }

    private double _progressValue;
    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            this.RaiseAndSetIfChanged(ref _progressValue, value);
        }
    }

    private string _action = "Install";
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

            if (string.IsNullOrWhiteSpace(InstallLocation))
                throw new InvalidOperationException("Install location is not set.");

            if (RestartElevatedIfNeeded())
                return;

            Message = $"{Action} In Progress... Do not close this window.";
            ProgressVisible = "True";
            ProgressValue = 0;

            if (!Directory.Exists(InstallLocation))
            {
                Directory.CreateDirectory(InstallLocation);
            }
            UpdateProgress(5);

            // Fetch release info from GitHub
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");
            var releaseInfoResponse = await httpClient.GetAsync(releaseInfoUrl);
            releaseInfoResponse.EnsureSuccessStatusCode();
            var releaseInfo = await releaseInfoResponse.Content.ReadAsStringAsync();
            var release = JsonDocument.Parse(releaseInfo);
            UpdateProgress(10);

            // Download FlightDeck.exe
            string? launcherUrl = null;
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

            await DownloadFile(launcherUrl, Path.Combine(InstallLocation, $"{launcherName}.exe"), 10, 55);

            // Download new installer with a different name
            string? installerUrl = null;
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
            await DownloadFile(installerUrl, newInstallerPath, 65, 20);

            Message = "Writing FlightDeck settings...";
            UpdateProgress(86);
            await Task.Delay(50);

            // Create config file in Local APPDATA
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), launcherName);
            await Task.Run(() => CreateConfigFile(Path.Combine(localAppDataPath, configFileName)));
            UpdateProgress(90);
            await Task.Delay(50);

            Message = "Creating shortcuts...";
            if (IsShortcutEnabled)
            {
                try
                {
                    await RunWithTimeoutAsync(
                        () => CreateShortcut(Path.Combine(InstallLocation, $"{launcherName}.exe"), launcherName),
                        TimeSpan.FromSeconds(5),
                        "Shortcut creation timed out.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Shortcut creation failed: {ex.Message}");
                }
            }
            UpdateProgress(94);
            await Task.Delay(50);

            Message = "Finishing installation...";
            UpdateProgress(98);
            await Task.Delay(50);

            UpdateProgress(100);
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
            Console.WriteLine($"Install failed: {ex.Message}");
            ProgressValue = 0;
            Message = $"Please ensure FlightDeck is not running and you are connected to the internet.";
            TextColor = "Red";
        }
    }

    private static async Task RunWithTimeoutAsync(Action action, TimeSpan timeout, string timeoutMessage)
    {
        var task = Task.Run(action);
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        if (completedTask != task)
            throw new TimeoutException(timeoutMessage);

        await task;
    }

    private async Task DownloadFile(string url, string filePath, double startPercent, double percentSpan)
    {
        Message = $"Downloading {Path.GetFileName(filePath)}...";
        var tempFilePath = $"{filePath}.download";

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
                UpdateProgress(startPercent + (downloadProgress * percentSpan));
            }
        });

        using (var stream = await response.Content.ReadAsStreamAsync())
        {
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                await CopyToAsync(stream, fileStream, progress);
            }
        }

        File.Move(tempFilePath, filePath, true);
        UpdateProgress(startPercent + percentSpan);
        Message = $"Downloaded {Path.GetFileName(filePath)}.";
        await Task.Delay(50);
    }

    private async Task CopyToAsync(Stream source, Stream destination, IProgress<long>? progress = null, int bufferSize = 81920)
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
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
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
        string? directory = Path.GetDirectoryName(configFilePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(configFilePath))
            File.WriteAllText(configFilePath, "{}");
    }

    private void UpdateProgress(double progress)
    {
        ProgressValue = Math.Clamp(progress, 0, 100);
    }

    public async void UninstallLauncher()
    {
        try
        {
            TextColor = "Gray";

            if (string.IsNullOrWhiteSpace(InstallLocation))
                throw new InvalidOperationException("Install location is not set.");

            if (RestartElevatedIfNeeded())
                return;

            Message = "Uninstalling... Do not close this window.";
            ProgressVisible = "True";
            ProgressValue = 0;

            if (IsShortcutEnabled)
            {
                DeleteShortcut(launcherName);
                UpdateProgress(30);
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
            UpdateProgress(100);
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
            ProgressValue = 0;
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
