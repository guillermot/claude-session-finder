#!/bin/bash
# Renders the macOS icon assets from the same SVG art the Windows .ico is drawn from.
#
# There is no SVG rasteriser on a stock Mac, so Quick Look is used as one. It has a trap worth
# writing down: qlmanage's -s flag sets the size of the canvas it thumbnails into, not the scale it
# draws at, and for a small document it leaves the art at a fraction of that canvas in the corner.
# Only the largest size renders the way the flag suggests. So every layer is rendered once at 1024
# and reduced with sips, which is a better reduction than Quick Look would have done anyway.
#
# The two source drawings are still used the way their own comments say to: app-16.svg has snapped
# geometry and thicker strokes and is what the 16 and 32 pixel layers come from, and app.svg is used
# from 64 upwards. Rendering the large drawing small spreads its 1.5 pixel strokes over two pixels
# at half intensity and the lens stops being a ring — and that is as true of reducing a 1024 pixel
# render as it was of rendering small in the first place, because the proportion is what matters.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
art="$root/src/SessionFinder.Wpf/Assets"
out="$root/src/SessionFinder.Mac/Assets"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

render() {  # render <svg> <pixels> <destination>
  local source="$1" pixels="$2" destination="$3"

  # The viewBox is left alone, so only the rendered resolution changes and the geometry does not.
  sed -e 's/width="16" height="16"/width="1024" height="1024"/' "$source" > "$work/scaled.svg"

  qlmanage -t -s 1024 -o "$work" "$work/scaled.svg" >/dev/null 2>&1

  if [[ ! -f "$work/scaled.svg.png" ]]; then
    echo "Quick Look produced nothing for $source." >&2
    exit 1
  fi

  sips -z "$pixels" "$pixels" "$work/scaled.svg.png" --out "$destination" >/dev/null
  rm -f "$work/scaled.svg" "$work/scaled.svg.png"
}

mkdir -p "$out"
iconset="$work/ClaudeSessionFinder.iconset"
mkdir -p "$iconset"

render "$art/app-16.svg" 16   "$iconset/icon_16x16.png"
render "$art/app-16.svg" 32   "$iconset/icon_16x16@2x.png"
render "$art/app-16.svg" 32   "$iconset/icon_32x32.png"
render "$art/app.svg"    64   "$iconset/icon_32x32@2x.png"
render "$art/app.svg"    128  "$iconset/icon_128x128.png"
render "$art/app.svg"    256  "$iconset/icon_128x128@2x.png"
render "$art/app.svg"    256  "$iconset/icon_256x256.png"
render "$art/app.svg"    512  "$iconset/icon_256x256@2x.png"
render "$art/app.svg"    512  "$iconset/icon_512x512.png"
render "$art/app.svg"    1024 "$iconset/icon_512x512@2x.png"

iconutil --convert icns "$iconset" --output "$out/app.icns"

# The menu-bar icon is the small drawing at twice the height the bar draws at, so that it is sharp
# on a Retina display. It is deliberately not a template image: the art is a mid-blue silhouette
# chosen to hold against both a light and a dark bar, and letting the system tint it flat would
# throw away the internal contrast between the blue body and the white lens.
render "$art/app-16.svg" 36 "$out/tray.png"

echo "Wrote $out/app.icns and $out/tray.png"
