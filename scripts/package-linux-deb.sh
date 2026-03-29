#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

project_path="${PROJECT_PATH:-src/Clever.TokenMap.App/Clever.TokenMap.App.csproj}"
configuration="${CONFIGURATION:-Release}"
runtime_identifier="${RUNTIME_IDENTIFIER:-linux-x64}"
app_name="${APP_NAME:-TokenMap}"
package_name="${PACKAGE_NAME:-tokenmap}"
maintainer="${MAINTAINER:-Clever.pro}"
section_name="${SECTION_NAME:-devel}"
output_root="${OUTPUT_ROOT:-.artifacts/linux-x64}"
package_version="${PACKAGE_VERSION:-}"
artifact_name="${ARTIFACT_NAME:-}"
install_root="${INSTALL_ROOT:-/usr/lib/tokenmap}"
desktop_entry_name="${DESKTOP_ENTRY_NAME:-tokenmap.desktop}"
icon_source_path="${ICON_SOURCE_PATH:-src/Clever.TokenMap.App/Assets/app-icon.svg}"
launcher_source_path="${LAUNCHER_SOURCE_PATH:-packaging/linux/tokenmap}"
desktop_source_path="${DESKTOP_SOURCE_PATH:-packaging/linux/tokenmap.desktop}"
description_summary="${DESCRIPTION_SUMMARY:-Local source-tree analysis}"
description_body="${DESCRIPTION_BODY:-TokenMap lets you inspect a codebase as both a tree and a treemap, measured by tokens, non-empty lines, or file size.}"
deb_depends="${DEB_DEPENDS:-libx11-6, libice6, libsm6, libfontconfig1, ca-certificates, tzdata, libc6, libgcc-s1 | libgcc1, libgssapi-krb5-2, libstdc++6, zlib1g, libssl3 | libssl1.1 | libssl1.0.2 | libssl1.0.0, libicu74 | libicu72 | libicu71 | libicu70 | libicu69 | libicu68 | libicu67 | libicu66 | libicu65 | libicu63 | libicu60 | libicu57 | libicu55 | libicu52}"

project_full_path="${repo_root}/${project_path}"
output_root_full_path="${repo_root}/${output_root}"
publish_directory_path="${output_root_full_path}/publish"
temporary_work_root="$(mktemp -d "${TMPDIR:-/tmp}/tokenmap-deb.XXXXXX")"
trap 'rm -rf "${temporary_work_root}"' EXIT
staging_directory_path="${temporary_work_root}/staging"
debian_directory_path="${staging_directory_path}/DEBIAN"
package_install_directory_path="${staging_directory_path}${install_root}"
launcher_install_path="${staging_directory_path}/usr/bin/${package_name}"
desktop_install_path="${staging_directory_path}/usr/share/applications/${desktop_entry_name}"
icon_install_path="${staging_directory_path}/usr/share/icons/hicolor/scalable/apps/${package_name}.svg"
icon_source_full_path="${repo_root}/${icon_source_path}"
launcher_source_full_path="${repo_root}/${launcher_source_path}"
desktop_source_full_path="${repo_root}/${desktop_source_path}"

require_command() {
    local command_name="$1"

    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Required command '${command_name}' was not found." >&2
        exit 1
    fi
}

assert_compatible_dotnet_sdk() {
    require_command dotnet

    if (cd "${repo_root}" && dotnet --version >/dev/null 2>&1); then
        return
    fi

    local required_sdk_version=""
    if [[ -f "${repo_root}/global.json" ]]; then
        required_sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${repo_root}/global.json" | head -n 1)"
    fi

    echo "A compatible .NET SDK for this repository was not found." >&2
    if [[ -n "${required_sdk_version}" ]]; then
        echo "global.json requires SDK '${required_sdk_version}'." >&2
    fi

    echo "Installed SDKs:" >&2
    dotnet --list-sdks >&2 || true
    exit 1
}

resolve_path() {
    local raw_path="$1"

    case "${raw_path}" in
        /*)
            printf '%s\n' "${raw_path}"
            ;;
        *)
            printf '%s\n' "${repo_root}/${raw_path}"
            ;;
    esac
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

map_runtime_identifier_to_architecture() {
    local runtime_id="$1"

    case "${runtime_id}" in
        linux-x64)
            echo "amd64"
            ;;
        linux-arm64)
            echo "arm64"
            ;;
        *)
            echo "Unsupported RuntimeIdentifier '${runtime_id}'." >&2
            exit 1
            ;;
    esac
}

require_command dpkg-deb

assembly_name="$(basename "${project_full_path}" .csproj)"
assembly_name_value="$(get_project_value '//AssemblyName')"
project_version="$(get_project_value '//Version')"
provided_publish_directory="${PUBLISH_DIRECTORY:-}"

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

architecture="$(map_runtime_identifier_to_architecture "${runtime_identifier}")"
package_file_path="${output_root_full_path}/${artifact_name}.deb"
temporary_package_file_path="${temporary_work_root}/${artifact_name}.deb"
if [[ -n "${provided_publish_directory}" ]]; then
    publish_directory_path="$(resolve_path "${provided_publish_directory}")"
fi
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

rm -f "${package_file_path}"
mkdir -p \
    "${output_root_full_path}" \
    "${debian_directory_path}" \
    "${package_install_directory_path}" \
    "$(dirname "${launcher_install_path}")" \
    "$(dirname "${desktop_install_path}")" \
    "$(dirname "${icon_install_path}")"

if [[ -z "${provided_publish_directory}" ]]; then
    assert_compatible_dotnet_sdk
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
else
    echo "Using existing publish output at: ${publish_directory_path}"
fi

if [[ ! -f "${published_executable_path}" ]]; then
    echo "Published Linux executable was not found at '${published_executable_path}'." >&2
    exit 1
fi

cp -R "${publish_directory_path}/." "${package_install_directory_path}/"
cp "${launcher_source_full_path}" "${launcher_install_path}"
cp "${desktop_source_full_path}" "${desktop_install_path}"
cp "${icon_source_full_path}" "${icon_install_path}"

chmod 0755 "${launcher_install_path}" "${package_install_directory_path}/${assembly_name}"

installed_size_kib="$(du -sk "${staging_directory_path}" | awk '{print $1}')"

cat > "${debian_directory_path}/control" <<EOF
Package: ${package_name}
Version: ${package_version}
Section: ${section_name}
Priority: optional
Architecture: ${architecture}
Installed-Size: ${installed_size_kib}
Depends: ${deb_depends}
Maintainer: ${maintainer}
Description: ${description_summary}
 ${description_body}
EOF

chmod 0755 "${debian_directory_path}"
chmod 0644 "${debian_directory_path}/control" "${desktop_install_path}" "${icon_install_path}"

dpkg-deb --root-owner-group --build "${staging_directory_path}" "${temporary_package_file_path}" >/dev/null
cp "${temporary_package_file_path}" "${package_file_path}"

echo "Linux package created at: ${package_file_path}"
