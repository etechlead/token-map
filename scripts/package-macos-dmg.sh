#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

project_path="${PROJECT_PATH:-src/Clever.TokenMap.App/Clever.TokenMap.App.csproj}"
configuration="${CONFIGURATION:-Release}"
runtime_identifier="${RUNTIME_IDENTIFIER:-osx-arm64}"
bundle_name="${BUNDLE_NAME:-TokenMap}"
output_root="${OUTPUT_ROOT:-.artifacts/macos-arm64}"
artifact_name="${ARTIFACT_NAME:-}"
bundle_identifier="${BUNDLE_IDENTIFIER:-pro.clever.tokenmap}"
bundle_version="${BUNDLE_VERSION:-}"
icon_file_name="${ICON_FILE_NAME:-app-icon.icns}"
icon_source_path="${ICON_SOURCE_PATH:-src/Clever.TokenMap.App/Assets/${icon_file_name}}"

project_full_path="${repo_root}/${project_path}"
output_root_full_path="${repo_root}/${output_root}"
publish_directory_path="${output_root_full_path}/publish"
bundle_directory_path="${output_root_full_path}/${bundle_name}.app"
bundle_contents_path="${bundle_directory_path}/Contents"
bundle_macos_path="${bundle_contents_path}/MacOS"
bundle_resources_path="${bundle_contents_path}/Resources"
staging_directory_path="${output_root_full_path}/dmg-stage"
temporary_dmg_path="${output_root_full_path}/${artifact_name}-temp.dmg"
icon_source_full_path="${repo_root}/${icon_source_path}"

assembly_name="$(basename "${project_full_path}" .csproj)"

if command -v xmllint >/dev/null 2>&1; then
    assembly_name_value="$(xmllint --xpath 'string(//AssemblyName)' "${project_full_path}" 2>/dev/null || true)"
    version_value="$(xmllint --xpath 'string(//Version)' "${project_full_path}" 2>/dev/null || true)"

    if [[ -n "${assembly_name_value}" ]]; then
        assembly_name="${assembly_name_value}"
    fi

    if [[ -z "${bundle_version}" && -n "${version_value}" ]]; then
        bundle_version="${version_value}"
    fi
fi

if [[ -z "${bundle_version}" ]]; then
    bundle_version="1.0.0"
fi

if [[ -z "${artifact_name}" ]]; then
    artifact_name="TokenMap-macos-arm64-${bundle_version}-unsigned"
fi

dmg_path="${output_root_full_path}/${artifact_name}.dmg"
temporary_dmg_path="${output_root_full_path}/${artifact_name}-temp.dmg"

published_executable_path="${publish_directory_path}/${assembly_name}"

rm -rf "${output_root_full_path}"
mkdir -p "${bundle_macos_path}" "${bundle_resources_path}" "${staging_directory_path}"

echo "Publishing ${project_full_path} for ${runtime_identifier}..."
dotnet publish "${project_full_path}" \
    -c "${configuration}" \
    -r "${runtime_identifier}" \
    --self-contained true \
    -p:UseAppHost=true \
    -p:Version="${bundle_version}" \
    -o "${publish_directory_path}"

if [[ ! -f "${published_executable_path}" ]]; then
    echo "Published macOS executable was not found at '${published_executable_path}'." >&2
    exit 1
fi

cp -R "${publish_directory_path}/." "${bundle_macos_path}/"
chmod +x "${bundle_macos_path}/${assembly_name}"

icon_plist_entries=""
if [[ -f "${icon_source_full_path}" ]]; then
    cp "${icon_source_full_path}" "${bundle_resources_path}/${icon_file_name}"
    icon_plist_entries=$(cat <<EOF
    <key>CFBundleIconFile</key>
    <string>${icon_file_name}</string>
EOF
)
fi

cat > "${bundle_contents_path}/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>${bundle_name}</string>
    <key>CFBundleExecutable</key>
    <string>${assembly_name}</string>
    <key>CFBundleIdentifier</key>
    <string>${bundle_identifier}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>${bundle_name}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
${icon_plist_entries}
    <key>CFBundleShortVersionString</key>
    <string>${bundle_version}</string>
    <key>CFBundleVersion</key>
    <string>${bundle_version}</string>
    <key>NSHighResolutionCapable</key>
    <true />
</dict>
</plist>
EOF

cp -R "${bundle_directory_path}" "${staging_directory_path}/"
ln -s /Applications "${staging_directory_path}/Applications"

hdiutil create \
    -volname "${bundle_name}" \
    -srcfolder "${staging_directory_path}" \
    -ov \
    -format UDRW \
    "${temporary_dmg_path}"

attach_output="$(hdiutil attach \
    -readwrite \
    -noverify \
    -noautoopen \
    "${temporary_dmg_path}")"

mount_directory_path="$(printf '%s\n' "${attach_output}" | awk '/\/Volumes\// {print $NF}' | tail -n 1)"
mounted_volume_name="$(basename "${mount_directory_path}")"

if [[ -z "${mount_directory_path}" || ! -d "${mount_directory_path}" ]]; then
    echo "Mounted DMG volume path could not be determined." >&2
    exit 1
fi

if command -v osascript >/dev/null 2>&1; then
    rm -f "${mount_directory_path}/Applications"

    if ! osascript <<EOF
tell application "Finder"
    tell disk "${mounted_volume_name}"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        set bounds of container window to {120, 120, 760, 420}
        set theViewOptions to the icon view options of container window
        set arrangement of theViewOptions to not arranged
        set icon size of theViewOptions to 128
        set text size of theViewOptions to 14

        if not (exists alias file "Applications" of disk "${mounted_volume_name}") then
            make new alias file at disk "${mounted_volume_name}" to POSIX file "/Applications"
        end if

        set position of item "${bundle_name}.app" of disk "${mounted_volume_name}" to {160, 150}
        set position of item "Applications" of disk "${mounted_volume_name}" to {460, 150}
        update without registering applications
        delay 1
        close
    end tell
end tell
EOF
    then
        echo "Finder customization failed; restoring the fallback Applications link." >&2
        ln -s /Applications "${mount_directory_path}/Applications"
    fi
fi

sync
hdiutil detach "${mount_directory_path}" -quiet

hdiutil convert \
    "${temporary_dmg_path}" \
    -ov \
    -format UDZO \
    -imagekey zlib-level=9 \
    -o "${dmg_path}" \
    >/dev/null

rm -f "${temporary_dmg_path}"

echo "macOS bundle created at: ${bundle_directory_path}"
echo "DMG created at: ${dmg_path}"
