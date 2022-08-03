Wasabito is an attempt to completely redesign Wasabi Wallet to make it reach it's full potential:


See the Introduction wiki page and Roadmap for more information.

<p align="center">
<img src="https://user-images.githubusercontent.com/51679301/169354261-c894bac0-27f2-4a29-8470-a7519963a4b5.jpg">
</p>

<h3 align="center">
    An open-source, non-custodial, privacy-focused Bitcoin wallet for desktop.
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
