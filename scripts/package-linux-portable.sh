#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

project_path="${PROJECT_PATH:-src/Clever.TokenMap.App/Clever.TokenMap.App.csproj}"
configuration="${CONFIGURATION:-Release}"
runtime_identifier="${RUNTIME_IDENTIFIER:-linux-x64}"
app_name="${APP_NAME:-TokenMap}"
portable_suffix="${PORTABLE_SUFFIX:--portable}"
output_root="${OUTPUT_ROOT:-.artifacts/linux-x64}"
package_version="${PACKAGE_VERSION:-}"
artifact_name="${ARTIFACT_NAME:-}"
portable_app_directory_name="${PORTABLE_APP_DIRECTORY_NAME:-app}"
launcher_source_path="${LAUNCHER_SOURCE_PATH:-packaging/linux/tokenmap}"

project_full_path="${repo_root}/${project_path}"
output_root_full_path="${repo_root}/${output_root}"
publish_directory_path="${output_root_full_path}/publish"
portable_output_root_path="${output_root_full_path}/portable"
launcher_source_full_path="${repo_root}/${launcher_source_path}"

require_command() {
    local command_name="$1"

    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Required command '${command_name}' was not found." >&2
        exit 1
    fi
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
require_command tar

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
    artifact_name="${app_name}-${runtime_identifier}-${package_version}${portable_suffix}"
fi

portable_root_path="${portable_output_root_path}/${artifact_name}"
portable_app_directory_path="${portable_root_path}/${portable_app_directory_name}"
portable_launcher_path="${portable_root_path}/tokenmap"
portable_archive_path="${output_root_full_path}/${artifact_name}.tar.gz"
published_executable_path="${publish_directory_path}/${assembly_name}"

if [[ ! -f "${launcher_source_full_path}" ]]; then
    echo "Linux launcher template was not found at '${launcher_source_full_path}'." >&2
    exit 1
fi

rm -f "${portable_archive_path}"
rm -rf "${publish_directory_path}" "${portable_root_path}"
mkdir -p "${portable_app_directory_path}"

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

cp -R "${publish_directory_path}/." "${portable_app_directory_path}/"
cp "${launcher_source_full_path}" "${portable_launcher_path}"

chmod 0755 "${portable_launcher_path}" "${portable_app_directory_path}/${assembly_name}"

tar -czf "${portable_archive_path}" -C "${portable_output_root_path}" "${artifact_name}"

echo "Linux portable archive created at: ${portable_archive_path}"
