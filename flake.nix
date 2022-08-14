{
  description = "Wasabito development flake";
  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
  };

  outputs = { self, nixpkgs }: 
    with nixpkgs.legacyPackages.x86_64-linux;
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs {
	    inherit system;
      };
      libs = [
        xorg.libX11
        xorg.libX11.dev
        xorg.libICE
        xorg.libSM
        fontconfig.lib
      ];
      deps = [
        bitcoind  # Wasabi has a integration with bitcoind and this is required during testing
        tor       # Tor is required to run Wasabi
        hwi       # HWI is required during testing and in case we have a hardware wallet
      ];
      # AvaloniaUI comes with a skiaSharp library (there has to be a better way to do this)
      skiaSharp =toString ./. + "/WalletWasabi.Fluent.Desktop/bin/Debug/net6.0/runtimes/linux-x64/native";
    in {
      devShell.x86_64-linux =
        mkShell {
          name = "dotnet-env";
          packages = [];
          buildInputs = libs ++ deps ++ [ dotnet-sdk_6 ];
    
          DOTNET_CLI_TELEMETRY_OPTOUT=1;
          LD_LIBRARY_PATH = "${skiaSharp};${lib.makeLibraryPath libs}";
        };
    };
}
