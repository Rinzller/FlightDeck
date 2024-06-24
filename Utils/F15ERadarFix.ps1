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
    Start-Service w32time
    # Force time synchronization with internet time servers
    w32tm /resync
    Write-Host "System time synchronized with internet time servers."
}

# Register an event handler for script termination
Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
    Cleanup
}

# Stop the Windows Time service to prevent automatic time synchronization
Stop-Service w32time -Force

# Define the date and time to set
$NewDate = "2023-06-13 00:00:00"

# Convert the string to a DateTime object
$DateTime = [DateTime]::ParseExact($NewDate, "yyyy-MM-dd HH:mm:ss", $null)

# Set the system date and time
Set-Date -Date $DateTime

Write-Host "System date and time have been set to $NewDate."

# Trap statement to catch termination
trap {
    Write-Host "An error occurred: $_"
    Cleanup
    break
}

# Keep the script running until terminated
while ($true) {
    Start-Sleep -Seconds 1
}
