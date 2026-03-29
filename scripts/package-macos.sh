#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

source "${script_dir}/packaging-metadata.sh"
initialize_packaging_metadata "${repo_root}"

project_path="${PROJECT_PATH:-$(get_packaging_metadata_value '//PackagingMetadata/ProjectPath')}"
configuration="${CONFIGURATION:-Release}"
runtime_identifier="${RUNTIME_IDENTIFIER:-osx-arm64}"
bundle_name="${BUNDLE_NAME:-$(get_packaging_metadata_value '//PackagingMetadata/AppName')}"
output_root="${OUTPUT_ROOT:-.artifacts/macos-arm64}"
bundle_identifier="${BUNDLE_IDENTIFIER:-$(get_packaging_metadata_value '//PackagingMetadata/MacOS/BundleIdentifier')}"
bundle_version="${BUNDLE_VERSION:-}"
dmg_artifact_name="${DMG_ARTIFACT_NAME:-}"
zip_artifact_name="${ZIP_ARTIFACT_NAME:-}"
icon_file_name="${ICON_FILE_NAME:-$(get_packaging_metadata_value '//PackagingMetadata/MacOS/IconFileName')}"
icon_source_path="${ICON_SOURCE_PATH:-$(get_packaging_metadata_value '//PackagingMetadata/MacOS/IconSourcePath')}"
applications_link_name="${APPLICATIONS_LINK_NAME:-$(get_packaging_metadata_value '//PackagingMetadata/MacOS/ApplicationsLinkName')}"
applications_icon_source_full_path="${APPLICATIONS_ICON_SOURCE_PATH:-$(get_packaging_metadata_value '//PackagingMetadata/MacOS/ApplicationsIconSourcePath')}"

project_full_path="${repo_root}/${project_path}"
output_root_full_path="${repo_root}/${output_root}"
publish_directory_path="${output_root_full_path}/publish"
bundle_directory_path="${output_root_full_path}/${bundle_name}.app"
bundle_contents_path="${bundle_directory_path}/Contents"
bundle_macos_path="${bundle_contents_path}/MacOS"
bundle_resources_path="${bundle_contents_path}/Resources"
staging_directory_path="${output_root_full_path}/dmg-stage"
icon_source_full_path="${repo_root}/${icon_source_path}"

assembly_name="$(basename "${project_full_path}" .csproj)"
assembly_name_value="$(get_xml_value "${project_full_path}" '//AssemblyName')"
repo_version="$(get_repo_version)"

if [[ -n "${assembly_name_value}" ]]; then
    assembly_name="${assembly_name_value}"
fi

if [[ -z "${bundle_version}" ]]; then
    bundle_version="${repo_version}"
fi

if [[ -z "${dmg_artifact_name}" ]]; then
    dmg_artifact_name="TokenMap-macos-arm64-${bundle_version}-unsigned"
fi

if [[ -z "${zip_artifact_name}" ]]; then
    zip_artifact_name="TokenMap-macos-arm64-${bundle_version}-portable-unsigned"
fi

dmg_path="${output_root_full_path}/${dmg_artifact_name}.dmg"
zip_path="${output_root_full_path}/${zip_artifact_name}.zip"
temporary_dmg_path="${output_root_full_path}/${dmg_artifact_name}-temp.dmg"

published_executable_path="${publish_directory_path}/${assembly_name}"

set_custom_file_icon() {
    local icon_path="$1"
    local target_path="$2"
    local temp_icon_base=""
    local temp_icon_path=""
    local temp_resource_path=""

    if ! command -v sips >/dev/null 2>&1 || ! command -v DeRez >/dev/null 2>&1 || ! command -v Rez >/dev/null 2>&1 || ! command -v SetFile >/dev/null 2>&1; then
        return 1
    fi

    temp_icon_base="$(mktemp "${TMPDIR:-/tmp}/tokenmap-file-icon.XXXXXX")"
    temp_icon_path="${temp_icon_base}.icns"
    temp_resource_path="$(mktemp "${TMPDIR:-/tmp}/tokenmap-file-icon-rsrc.XXXXXX")"

    mv "${temp_icon_base}" "${temp_icon_path}"
    cp "${icon_path}" "${temp_icon_path}"
    sips -i "${temp_icon_path}" >/dev/null
    DeRez -only icns "${temp_icon_path}" > "${temp_resource_path}"
    Rez -append "${temp_resource_path}" -o "${target_path}"
    SetFile -a C "${target_path}"

    rm -f "${temp_icon_path}" "${temp_resource_path}"
}

create_applications_drop_link() {
    local target_directory_path="$1"
    local link_path="${target_directory_path}/${applications_link_name}"

    rm -f "${link_path}"

    if command -v osascript >/dev/null 2>&1; then
        if osascript <<EOF
tell application "Finder"
    make new alias file at POSIX file "${target_directory_path}" to POSIX file "/Applications"
    set name of result to "${applications_link_name}"
end tell
EOF
        then
            if [[ -f "${applications_icon_source_full_path}" ]]; then
                if ! set_custom_file_icon "${applications_icon_source_full_path}" "${link_path}"; then
                    echo "Applying a custom icon to the Applications drop-link failed; keeping the default alias icon." >&2
                fi
            fi

            return 0
        fi
    fi

    ln -s /Applications "${link_path}"
}

ad_hoc_sign_bundle() {
    local bundle_path="$1"

    if ! command -v codesign >/dev/null 2>&1; then
        return 1
    fi

    codesign --force --deep -s - "${bundle_path}"
}

initialize_dmg_layout() {
    # Finder stores item positions relative to the content area, so keep the
    # layout in a few semantic values and derive the rest in one place.
    dmg_window_left=120
    dmg_window_top=120
    dmg_window_width=640
    dmg_window_height=380
    dmg_icon_size=128
    dmg_text_size=13
    dmg_item_center_gap=300
    dmg_item_y=108

    dmg_window_right=$((dmg_window_left + dmg_window_width))
    dmg_window_bottom=$((dmg_window_top + dmg_window_height))

    local content_center_x=$((dmg_window_width / 2))
    dmg_left_item_x=$((content_center_x - dmg_item_center_gap / 2))
    dmg_right_item_x=$((content_center_x + dmg_item_center_gap / 2))
}

customize_dmg_finder_layout() {
    local mounted_volume_name="$1"

    if ! command -v osascript >/dev/null 2>&1; then
        return 1
    fi

    osascript <<EOF
tell application "Finder"
    tell disk "${mounted_volume_name}"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        set bounds of container window to {${dmg_window_left}, ${dmg_window_top}, ${dmg_window_right}, ${dmg_window_bottom}}
        set theViewOptions to the icon view options of container window
        set arrangement of theViewOptions to not arranged
        set icon size of theViewOptions to ${dmg_icon_size}
        set text size of theViewOptions to ${dmg_text_size}

        if not (exists item "Applications") then
            error "Applications link was not found."
        end if

        set position of item "${bundle_name}.app" to {${dmg_left_item_x}, ${dmg_item_y}}
        set position of item "Applications" to {${dmg_right_item_x}, ${dmg_item_y}}
        update without registering applications
        delay 2
        close
        open
        update without registering applications
        delay 2
        close
    end tell
end tell
EOF
}

initialize_dmg_layout

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

if ! ad_hoc_sign_bundle "${bundle_directory_path}"; then
    echo "Ad-hoc signing the macOS app bundle failed." >&2
    exit 1
fi

if ! command -v ditto >/dev/null 2>&1; then
    echo "The 'ditto' tool is required to package the macOS ZIP artifact." >&2
    exit 1
fi

ditto -c -k --keepParent "${bundle_directory_path}" "${zip_path}"

cp -R "${bundle_directory_path}" "${staging_directory_path}/"
create_applications_drop_link "${staging_directory_path}"

hdiutil create \
    -volname "${bundle_name}" \
    -srcfolder "${staging_directory_path}" \
    -ov \
    -format UDRW \
    -fs HFS+ \
    "${temporary_dmg_path}"

attach_output="$(hdiutil attach \
    -readwrite \
    -noverify \
    -noautoopen \
    "${temporary_dmg_path}")"

mount_directory_path="$(printf '%s\n' "${attach_output}" | awk -F '\t' '/\/Volumes\// {print $NF}' | tail -n 1)"
mounted_volume_name="$(basename "${mount_directory_path}")"

if [[ -z "${mount_directory_path}" || ! -d "${mount_directory_path}" ]]; then
    echo "Mounted DMG volume path could not be determined." >&2
    exit 1
fi

if ! customize_dmg_finder_layout "${mounted_volume_name}"; then
    echo "Finder customization failed; keeping the fallback DMG layout." >&2
fi

if [[ -f "${icon_source_full_path}" ]] && command -v SetFile >/dev/null 2>&1; then
    cp "${icon_source_full_path}" "${mount_directory_path}/.VolumeIcon.icns"
    SetFile -a C "${mount_directory_path}"
    SetFile -a V "${mount_directory_path}/.VolumeIcon.icns"
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

if [[ -f "${icon_source_full_path}" ]]; then
    if ! set_custom_file_icon "${icon_source_full_path}" "${dmg_path}"; then
        echo "Applying a custom icon to the DMG file failed; keeping the default file icon." >&2
    fi
fi

echo "macOS bundle created at: ${bundle_directory_path}"
echo "ZIP created at: ${zip_path}"
echo "DMG created at: ${dmg_path}"
