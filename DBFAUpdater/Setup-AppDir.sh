#!/bin/sh
dotnet restore -r linux-x64
dotnet publish -c Release -r linux-x64 --self-contained true
HERE="$(dirname "$(readlink -f "${0}")")"
mkdir -p "$HERE"/bin/AppDir/usr/bin
cp "$HERE"/bin/Release/net10.0/linux-x64/publish/* "$HERE"/bin/AppDir/usr/bin/
cp "$HERE"/dbfaupd.desktop "$HERE"/bin/AppDir/
cp "$HERE"/Assets/dbfainstaller.png "$HERE"/bin/AppDir/
cp "$HERE"/AppRun.sh "$HERE"/bin/AppDir/AppRun