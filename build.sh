#!bin/bash

BUILD_PATH="$(realpath .)"
ResonitePath="$(realpath .)/ResoniteAssemblies/"

# setup Resonite's reference assemblies
mkdir -p "$ResonitePath" && cd "$ResonitePath" || exit 1
curl https://github.com/mpmxyz/ResoniteAssemblies/raw/refs/heads/main/Assemblies.zip || exit 2
unzip Assemblies.zip || exit 3
cd "$BUILD_PATH" || exit 4

# download packages
dotnet restore || exit 5

# build
dotnet build --no-restore --configuration Release || exit 6

# test
dotnet test --no-restore --no-build || exit 7

