#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/dist/win-x64}"
dotnet publish "$ROOT/src/GrabadorReuniones/GrabadorReuniones.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:PublishTrimmed=false \
  -p:DebugType=none \
  -p:EnableWindowsTargeting=true \
  -o "$OUT"
echo "Publicado en $OUT/GrabadorReuniones.exe"
