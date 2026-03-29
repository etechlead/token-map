#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

project_path="${PROJECT_PATH:-src/Clever.TokenMap.App/Clever.TokenMap.App.csproj}"
configuration="${CONFIGURATION:-Release}"
runtime_identifier="${RUNTIME_IDENTIFIER:-linux-x64}"
app_name="${APP_NAME:-TokenMap}"
output_root="${OUTPUT_ROOT:-.artifacts/linux-x64}"
publish_output_root="${PUBLISH_OUTPUT_ROOT:-.artifacts/linux-package-inputs/publish}"
package_version="${PACKAGE_VERSION:-}"
artifact_base_name="${ARTIFACT_BASE_NAME:-}"
deb_artifact_name="${DEB_ARTIFACT_NAME:-}"
portable_artifact_name="${PORTABLE_ARTIFACT_NAME:-}"

project_full_path="${repo_root}/${project_path}"
publish_directory_path="${repo_root}/${publish_output_root}/${runtime_identifier}"

get_project_value() {
    local xpath="$1"

    if command -v xmllint >/dev/null 2>&1; then
        xmllint --xpath "string(${xpath})" "${project_full_path}" 2>/dev/null || true
        return
    fi

    local element_name="${xpath##//}"
    sed -n "s:.*<${element_name}>\\(.*\\)</${element_name}>.*:\\1:p" "${project_full_path}" | head -n 1
}

project_version="$(get_project_value '//Version')"

if [[ -z "${package_version}" ]]; then
    package_version="${project_version}"
fi

if [[ -z "${package_version}" ]]; then
    package_version="0.1.1-local"
fi

if [[ -z "${artifact_base_name}" ]]; then
    artifact_base_name="${app_name}-${runtime_identifier}-${package_version}"
fi

if [[ -z "${deb_artifact_name}" ]]; then
    deb_artifact_name="${artifact_base_name}"
fi

if [[ -z "${portable_artifact_name}" ]]; then
    portable_artifact_name="${artifact_base_name}-portable"
fi

rm -rf "${publish_directory_path}"
mkdir -p "${publish_directory_path}"

echo "Publishing ${project_full_path} for ${runtime_identifier}..."
dotnet publish "${project_full_path}" \
    -c "${configuration}" \
    -r "${runtime_identifier}" \
    --self-contained true \
    -p:UseAppHost=true \
    -p:Version="${package_version}" \
    -o "${publish_directory_path}"

ARTIFACT_NAME="${deb_artifact_name}" \
PACKAGE_VERSION="${package_version}" \
OUTPUT_ROOT="${output_root}" \
PUBLISH_DIRECTORY="${publish_directory_path}" \
bash "${script_dir}/package-linux-deb.sh"

ARTIFACT_NAME="${portable_artifact_name}" \
PACKAGE_VERSION="${package_version}" \
OUTPUT_ROOT="${output_root}" \
PUBLISH_DIRECTORY="${publish_directory_path}" \
bash "${script_dir}/package-linux-portable.sh"
