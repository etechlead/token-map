#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

project_path="${PROJECT_PATH:-src/Clever.TokenMap.App/Clever.TokenMap.App.csproj}"
configuration="${CONFIGURATION:-Release}"
runtime_identifier="${RUNTIME_IDENTIFIER:-linux-x64}"
app_name="${APP_NAME:-TokenMap}"
package_name="${PACKAGE_NAME:-tokenmap}"
output_root="${OUTPUT_ROOT:-.artifacts/linux-x64}"
package_version="${PACKAGE_VERSION:-}"
artifact_name="${ARTIFACT_NAME:-}"
install_root="${INSTALL_ROOT:-usr/lib/tokenmap}"
desktop_entry_name="${DESKTOP_ENTRY_NAME:-tokenmap.desktop}"
icon_file_name="${ICON_FILE_NAME:-tokenmap.svg}"
icon_source_path="${ICON_SOURCE_PATH:-src/Clever.TokenMap.App/Assets/app-icon.svg}"
launcher_source_path="${LAUNCHER_SOURCE_PATH:-packaging/linux/tokenmap}"
desktop_source_path="${DESKTOP_SOURCE_PATH:-packaging/linux/tokenmap.desktop}"
appimagetool_url="${APPIMAGETOOL_URL:-https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage}"

project_full_path="${repo_root}/${project_path}"
output_root_full_path="${repo_root}/${output_root}"
publish_directory_path="${output_root_full_path}/publish"
tools_directory_path="${output_root_full_path}/tools"
appdir_path="${output_root_full_path}/${package_name}.AppDir"
appdir_install_directory_path="${appdir_path}/${install_root}"
appdir_bin_directory_path="${appdir_path}/usr/bin"
appdir_applications_directory_path="${appdir_path}/usr/share/applications"
appdir_icons_directory_path="${appdir_path}/usr/share/icons/hicolor/scalable/apps"
apprun_path="${appdir_path}/AppRun"
appdir_desktop_entry_path="${appdir_path}/${desktop_entry_name}"
appdir_icon_path="${appdir_path}/${icon_file_name}"
launcher_install_path="${appdir_bin_directory_path}/${package_name}"
desktop_install_path="${appdir_applications_directory_path}/${desktop_entry_name}"
icon_install_path="${appdir_icons_directory_path}/${icon_file_name}"
icon_source_full_path="${repo_root}/${icon_source_path}"
launcher_source_full_path="${repo_root}/${launcher_source_path}"
desktop_source_full_path="${repo_root}/${desktop_source_path}"
appimagetool_path="${tools_directory_path}/appimagetool-x86_64.AppImage"

require_command() {
    local command_name="$1"

    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Required command '${command_name}' was not found." >&2
        exit 1
    fi
}

download_file() {
    local url="$1"
    local destination_path="$2"

    require_command curl
    curl -L --fail --silent --show-error "${url}" -o "${destination_path}"
}

get_project_value() {
    local xpath="$1"

    if command -v xmllint >/dev/null 2>&1; then
        xmllint --xpath "string(${xpath})" "${project_full_path}" 2>/dev/null || true
        return
    fi

    local element_name="${xpath##//}"
    sed -n "s:.*<${element_name}>\\(.*\\)</${element_name}>.*:\\1:p" "${project_full_path}" | head -n 1
}

require_command dotnet

assembly_name="$(basename "${project_full_path}" .csproj)"
assembly_name_value="$(get_project_value '//AssemblyName')"
project_version="$(get_project_value '//Version')"

if [[ -n "${assembly_name_value}" ]]; then
    assembly_name="${assembly_name_value}"
fi

if [[ -z "${package_version}" ]]; then
    package_version="${project_version}"
fi

if [[ -z "${package_version}" ]]; then
    package_version="0.1.1-local"
fi

if [[ -z "${artifact_name}" ]]; then
    artifact_name="${app_name}-${runtime_identifier}-${package_version}"
fi

appimage_path="${output_root_full_path}/${artifact_name}.AppImage"
published_executable_path="${publish_directory_path}/${assembly_name}"

if [[ ! -f "${launcher_source_full_path}" ]]; then
    echo "Linux launcher template was not found at '${launcher_source_full_path}'." >&2
    exit 1
fi

if [[ ! -f "${desktop_source_full_path}" ]]; then
    echo "Linux desktop entry template was not found at '${desktop_source_full_path}'." >&2
    exit 1
fi

if [[ ! -f "${icon_source_full_path}" ]]; then
    echo "Linux application icon was not found at '${icon_source_full_path}'." >&2
    exit 1
fi

rm -f "${appimage_path}"
rm -rf "${publish_directory_path}" "${appdir_path}"
mkdir -p \
    "${output_root_full_path}" \
    "${tools_directory_path}" \
    "${appdir_install_directory_path}" \
    "${appdir_bin_directory_path}" \
    "${appdir_applications_directory_path}" \
    "${appdir_icons_directory_path}"

echo "Publishing ${project_full_path} for ${runtime_identifier}..."
dotnet publish "${project_full_path}" \
    -c "${configuration}" \
    -r "${runtime_identifier}" \
    --self-contained true \
    -p:UseAppHost=true \
    -p:Version="${package_version}" \
    -o "${publish_directory_path}"

if [[ ! -f "${published_executable_path}" ]]; then
    echo "Published Linux executable was not found at '${published_executable_path}'." >&2
    exit 1
fi

cp -R "${publish_directory_path}/." "${appdir_install_directory_path}/"
cp "${launcher_source_full_path}" "${launcher_install_path}"
cp "${launcher_source_full_path}" "${apprun_path}"
cp "${desktop_source_full_path}" "${desktop_install_path}"
cp "${desktop_source_full_path}" "${appdir_desktop_entry_path}"
cp "${icon_source_full_path}" "${icon_install_path}"
cp "${icon_source_full_path}" "${appdir_icon_path}"

chmod 0755 "${launcher_install_path}" "${apprun_path}" "${appdir_install_directory_path}/${assembly_name}"

if [[ ! -f "${appimagetool_path}" ]]; then
    download_file "${appimagetool_url}" "${appimagetool_path}"
fi

chmod 0755 "${appimagetool_path}"

export APPIMAGE_EXTRACT_AND_RUN=1
export ARCH=x86_64
export VERSION="${package_version}"

"${appimagetool_path}" "${appdir_path}" "${appimage_path}"

if [[ ! -f "${appimage_path}" ]]; then
    echo "Expected AppImage was not found at '${appimage_path}'." >&2
    exit 1
fi

echo "Linux AppImage created at: ${appimage_path}"
