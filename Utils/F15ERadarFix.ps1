# Ensure the script is running with administrative privileges
if (!([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator"))
{
    Write-Host "This script needs to be run as an Administrator."
    exit
}

# Define cleanup actions
function Cleanup {
    Write-Host "Performing cleanup actions..."
    # Start the Windows Time service
    Start-Service w32time -ErrorAction SilentlyContinue *>&1 | Out-Null
    # Force time synchronization with internet time servers
    w32tm /resync *>&1 | Out-Null
    Write-Host "System time synchronized with internet time servers."
}

# Register an event handler for script termination
Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
    Cleanup
} *>&1 | Out-Null

# Stop the Windows Time service to prevent automatic time synchronization
Stop-Service w32time -ErrorAction SilentlyContinue *>&1 | Out-Null

# Define the date and time to set
$NewDate = "2023-06-13 00:00:00"

# Convert the string to a DateTime object
$DateTime = [DateTime]::ParseExact($NewDate, "yyyy-MM-dd HH:mm:ss", $null)

# Set the system date and time
Set-Date -Date $DateTime -ErrorAction SilentlyContinue *>&1 | Out-Null

Write-Host "System date and time have been set to $NewDate."

# Use try...finally to ensure cleanup runs on termination
try {
    # Keep the script running until Enter is pressed
    Read-Host -Prompt "Press Enter to exit and sync time with the internet"
} finally {
    Cleanup
}
