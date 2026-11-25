#!/usr/bin/env bash
# Creates the five VecTiles NuGet packages in dependency order (optionally pushes them).
#
# Usage:
#   ./pack-nuget.sh                    # build all packages into artifacts/nugets
#   ./pack-nuget.sh --push <source> [api-key]
#                                      # build + push to a NuGet feed, e.g.:
#                                      #   ./pack-nuget.sh --push nuget.org $NUGET_API_KEY
#                                      # the api key defaults to the NUGET_API_KEY env variable
#
# The version of all five packages is set in Directory.Build.props (property "Version").
# Bump it there before releasing.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="$REPO_ROOT/artifacts/nugets"
CONFIG="Release"

# Some shells (e.g. inside Rider) fail to start dotnet without the system library path.
if ! dotnet --version >/dev/null 2>&1; then
    export LD_LIBRARY_PATH="/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

# Must be packed in this order, the later packages depend on the earlier ones.
PACKAGES=(
    "Source/Packages/VecTiles.Common.Package/VecTiles.Common.Package.csproj"
    "Source/Packages/VecTiles.OpenMapTiles.Package/VecTiles.OpenMapTiles.Package.csproj"
    "Source/Packages/VecTiles.Mapsforge.Package/VecTiles.Mapsforge.Package.csproj"
    "Source/Packages/VecTiles.Skia.Package/VecTiles.Skia.Package.csproj"
    "Source/Packages/VecTiles.Mapsui.Package/VecTiles.Mapsui.Package.csproj"
)

mkdir -p "$OUT_DIR"

# Remove previously built packages (same version) and the cached versions from
# the global NuGet package folder, so packing the same version repeatedly works.
rm -f "$OUT_DIR"/VecTiles.*.nupkg $OUT_DIR/*.snupkg 2>/dev/null || true
for pkg in vectiles.common vectiles.openmaptiles vectiles.mapsforge vectiles.skia vectiles.mapsui; do
    rm -rf "$HOME/.nuget/packages/$pkg"
done

for proj in "${PACKAGES[@]}"; do
    echo "=== Packing $proj"
    dotnet pack "$REPO_ROOT/$proj" -c "$CONFIG" -p:CreateNuGets=true -o "$OUT_DIR"
done

echo
echo "Packages created in $OUT_DIR:"
ls -1 "$OUT_DIR"/VecTiles.*.nupkg

if [[ "${1:-}" == "--push" ]]; then
    SOURCE="${2:?Usage: pack-nuget.sh --push <source> [api-key]}"
    API_KEY="${3:-${NUGET_API_KEY:-}}"
    echo
    for nupkg in "$OUT_DIR"/VecTiles.*.nupkg; do
        echo "=== Pushing $(basename "$nupkg") to $SOURCE"
        if [[ -n "$API_KEY" ]]; then
            dotnet nuget push "$nupkg" --source "$SOURCE" --api-key "$API_KEY"
        else
            dotnet nuget push "$nupkg" --source "$SOURCE"
        fi
    done
fi
