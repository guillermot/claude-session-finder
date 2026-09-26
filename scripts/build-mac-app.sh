#!/bin/bash
# Publishes the macOS head and assembles it into a .app bundle.
#
# The bundle is not a packaging nicety here, it is what makes the application work. LSUIElement in
# Info.plist is what keeps the launcher out of the Dock and the application switcher, and it is only
# read for a process the window server recognises as a bundled application — which a bare executable
# in a publish folder is not. The launch agent opens this bundle for the same reason.
#
# Signed ad hoc, which grants nothing but an identity. macOS decides whether to show a notification
# by who is asking, and an unsigned binary re-identifies itself on every build.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${1:-Release}"
runtime="${2:-osx-arm64}"

publish="$root/artifacts/publish"
bundle="$root/artifacts/ClaudeSessionFinder.app"

rm -rf "$publish" "$bundle"

dotnet publish "$root/src/SessionFinder.Mac" \
  -c "$configuration" \
  -r "$runtime" \
  -o "$publish" \
  --nologo

mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"

cp -R "$publish"/* "$bundle/Contents/MacOS/"
cp "$root/src/SessionFinder.Mac/Info.plist" "$bundle/Contents/Info.plist"
cp "$root/src/SessionFinder.Mac/Assets/app.icns" "$bundle/Contents/Resources/app.icns"

chmod +x "$bundle/Contents/MacOS/ClaudeSessionFinder"

codesign --force --deep --sign - "$bundle"

echo "Built $bundle"
