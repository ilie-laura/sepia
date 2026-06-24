# Azure Subscription Scanner CLI

A C# command-line tool to scan Azure subscriptions and list storage accounts with their blobs.

## Features
- Lists all Azure subscriptions you have access to
- Scans each subscription for storage accounts
- Lists all containers and blobs in each storage account
- Outputs results to both console and a text file (`azure_scan_results.txt`)

## Prerequisites
- .NET 8.0 or later
- Azure CLI installed and authenticated, OR valid Azure credentials configured
- Storage account access keys available

## Setup & Build

### 1. Install .NET (if not already installed)
Download from: https://dotnet.microsoft.com/download

### 2. Build the project
```bash
dotnet build
```

### 3. Restore dependencies
```bash
dotnet restore
```

## Running the Application

### Option 1: Using Azure CLI (recommended)
First, authenticate with Azure:
```bash
az login
```

Then run the application:
```bash
dotnet run
```

### Option 2: Direct execution
```bash
dotnet bin/Debug/net8.0/AzureScannerCLI.exe
```

## Output
Results are saved to `azure_scan_results.txt` in the application directory, and also printed to the console.

## Troubleshooting
- **Authentication fails**: Ensure you've run `az login` first
- **No subscriptions found**: Verify your Azure account has active subscriptions
- **Blob access denied**: Check storage account access keys and network permissions
