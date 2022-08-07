Wasabito is an attempt to completely redesign Wasabi Wallet to make it reach it's full potential:


![image](https://user-images.githubusercontent.com/127973/183272381-b040f84e-d5b7-42b5-b6a4-52ce7e0c438b.png)

<h3 align="center">
    An open-source, highly experimental, non-custodial, privacy-focused Bitcoin wallet for desktop.
</h3>

<br>
<br>


# Build From Source Code

### Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET 6.0 SDK: https://dotnet.microsoft.com/download
3. Optionally disable .NET's telemetry by executing in the terminal `export DOTNET_CLI_TELEMETRY_OPTOUT=1` on Linux and macOS or `setx DOTNET_CLI_TELEMETRY_OPTOUT 1` on Windows.

### Get Wasabi

Clone & Restore & Build

```sh
git clone https://github.com/zkSNACKs/WalletWasabi.git
cd WalletWasabi/WalletWasabi.Fluent.Desktop
dotnet build
```

### Run Wasabi

Run Wasabi with `dotnet run` from the `WalletWasabi.Fluent.Desktop` folder.

### Update Wasabi

```sh
git pull
```
