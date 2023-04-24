# cs2hx
 Given a C# DLL, generates Haxe externs OR automatic Haxe compile-time definitions

# Building
To build the `dll_reader/` executable, open the `.csproj` with Visual Studio, and build for release!

Alternatively, you can build from the command-line using `.Net` (Run this command in the `dll_reader/` folder):
```
dotnet build --configuration Release
```

# Test
This project is currently in development. To test what I'm testing:
```
cd test
haxe Test.hxml
```
