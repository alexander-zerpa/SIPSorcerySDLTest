{
  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs?ref=nixpkgs-unstable";
  };

  outputs = { self, nixpkgs }: 
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
    in {
      devShells.${system}.default = pkgs.mkShell rec {
        name = "SIPSorcerySDL2Test";
        packages = with pkgs; [
          dotnet-sdk_8
          SDL2
          SDL2.dev
          pjsip
          sngrep
        ];
        LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath packages;
      };
    };
}
