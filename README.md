# SIPSorcerySDL2Test

## Dependencies

* dotnet-sdk 8
* SDL2 libraries

## Setting up

### On PopOs

Make sure to have the submodules
```bash
git clone https://github.com/alexander-zerpa/SIPSorcerySDLTest.git --recurse-submodules
```

Install dependencies
```bash
sudo apt install dotnet-sdk-8.0 libsdl2-dev
```

Run
```bash
dotnet run --project ./src/SIPSorcerySDL2Test.csproj
```
### On Nix

Use flake.nix file to build shell
