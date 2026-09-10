#!/usr/bin/env bash
# Builds both distributables into dist/:
#   wiredrop-<version>-rh8_0-any.yak   for Rhino's Package Manager
#   WireDrop-<version>.zip             for manual install
set -euo pipefail
cd "$(dirname "$0")"

VERSION="${VERSION:-0.3.2}"
YAK="${YAK:-/Applications/Rhino 8.app/Contents/Resources/bin/yak}"

echo "==> test"
dotnet test tests/WireDrop.Tests --nologo -v q

echo "==> build"
rm -rf src/WireDrop/bin src/WireDrop/obj
dotnet build src/WireDrop -c Release --nologo -v q

rm -rf dist && mkdir -p dist/payload dist/zip
cp src/WireDrop/bin/Release/WireDrop.gha dist/payload/
cp src/WireDrop/bin/Release/WireDrop.gha dist/zip/
cp INSTALL.txt dist/zip/ 2>/dev/null || true

cat > dist/payload/manifest.yml <<YML
---
name: WireDrop
version: $VERSION
authors:
  - Yolanda Xing
url: https://github.com/YolandaXing210/wire-drop
description: >
  Pull a wire off any Grasshopper port, let go over empty canvas, and a panel opens
  listing every component that port can connect to. Type to narrow it, arrow keys to
  move, Enter to place the component already wired up. Works from inputs and outputs,
  and a single undo takes back both the component and the wire.
keywords:
  - grasshopper
  - canvas
  - wire
  - search
  - workflow
YML

echo "==> yak"
( cd dist/payload && "$YAK" build --platform any >/dev/null && mv ./*.yak .. )

echo "==> zip"
( cd dist/zip && zip -q -r "../WireDrop-$VERSION.zip" . )

rm -rf dist/payload dist/zip
ls -la dist
