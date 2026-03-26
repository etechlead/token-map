#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

project_path="${PROJECT_PATH:-src/Clever.TokenMap.App/Clever.TokenMap.App.csproj}"
configuration="${CONFIGURATION:-Release}"
runtime_identifier="${RUNTIME_IDENTIFIER:-osx-arm64}"
bundle_name="${BUNDLE_NAME:-TokenMap}"
output_root="${OUTPUT_ROOT:-artifacts/macos-arm64}"
artifact_name="${ARTIFACT_NAME:-TokenMap-macos-arm64}"
bundle_identifier="${BUNDLE_IDENTIFIER:-pro.clever.tokenmap}"
bundle_version="${BUNDLE_VERSION:-1.0.0}"

project_full_path="${repo_root}/${project_path}"
output_root_full_path="${repo_root}/${output_root}"
publish_directory_path="${output_root_full_path}/publish"
bundle_directory_path="${output_root_full_path}/${bundle_name}.app"
bundle_contents_path="${bundle_directory_path}/Contents"
bundle_macos_path="${bundle_contents_path}/MacOS"
bundle_resources_path="${bundle_contents_path}/Resources"
staging_directory_path="${output_root_full_path}/dmg-stage"
dmg_path="${output_root_full_path}/${artifact_name}.dmg"

assembly_name="$(basename "${project_full_path}" .csproj)"

if command -v xmllint >/dev/null 2>&1; then
    assembly_name_value="$(xmllint --xpath 'string(//AssemblyName)' "${project_full_path}" 2>/dev/null || true)"
    version_value="$(xmllint --xpath 'string(//Version)' "${project_full_path}" 2>/dev/null || true)"

    if [[ -n "${assembly_name_value}" ]]; then
        assembly_name="${assembly_name_value}"
    fi

    if [[ -n "${version_value}" ]]; then
        bundle_version="${version_value}"
    fi
fi

published_executable_path="${publish_directory_path}/${assembly_name}"

rm -rf "${output_root_full_path}"
mkdir -p "${bundle_macos_path}" "${bundle_resources_path}" "${staging_directory_path}"

echo "Publishing ${project_full_path} for ${runtime_identifier}..."
dotnet publish "${project_full_path}" \
    -c "${configuration}" \
    -r "${runtime_identifier}" \
    --self-contained true \
    -p:UseAppHost=true \
    -o "${publish_directory_path}"

if [[ ! -f "${published_executable_path}" ]]; then
    echo "Published macOS executable was not found at '${published_executable_path}'." >&2
    exit 1
fi

cp -R "${publish_directory_path}/." "${bundle_macos_path}/"
chmod +x "${bundle_macos_path}/${assembly_name}"

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
    -format UDZO \
    "${dmg_path}"

echo "macOS bundle created at: ${bundle_directory_path}"
echo "DMG created at: ${dmg_path}"
