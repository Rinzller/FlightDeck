# Ensure the script is running with administrative privileges
if (!([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator"))
{
    Write-Host "This script needs to be run as an Administrator."
    exit
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
