#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

source "${script_dir}/packaging-metadata.sh"
initialize_packaging_metadata "${repo_root}"

project_path="${PROJECT_PATH:-$(get_packaging_metadata_value '//PackagingMetadata/ProjectPath')}"
configuration="${CONFIGURATION:-Release}"
runtime_identifier="${RUNTIME_IDENTIFIER:-linux-x64}"
app_name="${APP_NAME:-$(get_packaging_metadata_value '//PackagingMetadata/AppName')}"
package_name="${PACKAGE_NAME:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/PackageName')}"
maintainer="${MAINTAINER:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/Maintainer')}"
section_name="${SECTION_NAME:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/SectionName')}"
portable_suffix="${PORTABLE_SUFFIX:--portable}"
output_root="${OUTPUT_ROOT:-.artifacts/linux-x64}"
publish_output_root="${PUBLISH_OUTPUT_ROOT:-.artifacts/linux-package-inputs/publish}"
package_version="${PACKAGE_VERSION:-}"
artifact_base_name="${ARTIFACT_BASE_NAME:-}"
deb_artifact_name="${DEB_ARTIFACT_NAME:-}"
portable_artifact_name="${PORTABLE_ARTIFACT_NAME:-}"
install_root="${INSTALL_ROOT:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/InstallRoot')}"
desktop_entry_name="${DESKTOP_ENTRY_NAME:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/DesktopEntryName')}"
icon_source_path="${ICON_SOURCE_PATH:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/IconSourcePath')}"
launcher_source_path="${LAUNCHER_SOURCE_PATH:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/LauncherSourcePath')}"
desktop_source_path="${DESKTOP_SOURCE_PATH:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/DesktopSourcePath')}"
portable_app_directory_name="${PORTABLE_APP_DIRECTORY_NAME:-app}"
description_summary="${DESCRIPTION_SUMMARY:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/DescriptionSummary')}"
description_body="${DESCRIPTION_BODY:-$(get_packaging_metadata_value '//PackagingMetadata/Linux/DescriptionBody')}"
deb_depends="${DEB_DEPENDS:-libx11-6, libice6, libsm6, libfontconfig1, ca-certificates, tzdata, libc6, libgcc-s1 | libgcc1, libgssapi-krb5-2, libstdc++6, zlib1g, libssl3 | libssl1.1 | libssl1.0.2 | libssl1.0.0, libicu74 | libicu72 | libicu71 | libicu70 | libicu69 | libicu68 | libicu67 | libicu66 | libicu65 | libicu63 | libicu60 | libicu57 | libicu55 | libicu52}"

project_full_path="${repo_root}/${project_path}"
output_root_full_path="${repo_root}/${output_root}"
publish_directory_path="${repo_root}/${publish_output_root}/${runtime_identifier}"
launcher_source_full_path="${repo_root}/${launcher_source_path}"
desktop_source_full_path="${repo_root}/${desktop_source_path}"
icon_source_full_path="${repo_root}/${icon_source_path}"

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

package_deb() {
    local assembly_name="$1"
    local package_file_path="${output_root_full_path}/${deb_artifact_name}.deb"
    local temporary_work_root
    temporary_work_root="$(mktemp -d "${TMPDIR:-/tmp}/tokenmap-deb.XXXXXX")"
    local staging_directory_path="${temporary_work_root}/staging"
    local debian_directory_path="${staging_directory_path}/DEBIAN"
    local package_install_directory_path="${staging_directory_path}${install_root}"
    local launcher_install_path="${staging_directory_path}/usr/bin/${package_name}"
    local desktop_install_path="${staging_directory_path}/usr/share/applications/${desktop_entry_name}"
    local icon_install_path="${staging_directory_path}/usr/share/icons/hicolor/scalable/apps/${package_name}.svg"
    local temporary_package_file_path="${temporary_work_root}/${deb_artifact_name}.deb"
    local published_executable_path="${publish_directory_path}/${assembly_name}"
    local installed_size_kib

    rm -f "${package_file_path}"
    mkdir -p \
        "${output_root_full_path}" \
        "${debian_directory_path}" \
        "${package_install_directory_path}" \
        "$(dirname "${launcher_install_path}")" \
        "$(dirname "${desktop_install_path}")" \
        "$(dirname "${icon_install_path}")"

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
Architecture: $(map_runtime_identifier_to_architecture "${runtime_identifier}")
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
    rm -rf "${temporary_work_root}"

    echo "Linux package created at: ${package_file_path}"
}

package_portable() {
    local assembly_name="$1"
    local portable_output_root_path="${output_root_full_path}/portable"
    local portable_root_path="${portable_output_root_path}/${portable_artifact_name}"
    local portable_app_directory_path="${portable_root_path}/${portable_app_directory_name}"
    local portable_launcher_path="${portable_root_path}/tokenmap"
    local portable_archive_path="${output_root_full_path}/${portable_artifact_name}.tar.gz"
    local published_executable_path="${publish_directory_path}/${assembly_name}"

    rm -f "${portable_archive_path}"
    rm -rf "${portable_root_path}"
    mkdir -p "${portable_app_directory_path}"

    if [[ ! -f "${published_executable_path}" ]]; then
        echo "Published Linux executable was not found at '${published_executable_path}'." >&2
        exit 1
    fi

    cp -R "${publish_directory_path}/." "${portable_app_directory_path}/"
    cp "${launcher_source_full_path}" "${portable_launcher_path}"

    chmod 0755 "${portable_launcher_path}" "${portable_app_directory_path}/${assembly_name}"

    tar -czf "${portable_archive_path}" -C "${portable_output_root_path}" "${portable_artifact_name}"

    echo "Linux portable archive created at: ${portable_archive_path}"
}

require_command dpkg-deb
require_command tar
assert_compatible_dotnet_sdk

assembly_name="$(basename "${project_full_path}" .csproj)"
assembly_name_value="$(get_xml_value "${project_full_path}" '//AssemblyName')"
repo_version="$(get_repo_version)"

if [[ -n "${assembly_name_value}" ]]; then
    assembly_name="${assembly_name_value}"
fi

if [[ -z "${package_version}" ]]; then
    package_version="${repo_version}"
fi

if [[ -z "${artifact_base_name}" ]]; then
    artifact_base_name="${app_name}-${runtime_identifier}-${package_version}"
fi

if [[ -z "${deb_artifact_name}" ]]; then
    deb_artifact_name="${artifact_base_name}"
fi

if [[ -z "${portable_artifact_name}" ]]; then
    portable_artifact_name="${artifact_base_name}${portable_suffix}"
fi

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

rm -rf "${publish_directory_path}"
mkdir -p "${publish_directory_path}" "${output_root_full_path}"

echo "Publishing ${project_full_path} for ${runtime_identifier}..."
dotnet publish "${project_full_path}" \
    -c "${configuration}" \
    -r "${runtime_identifier}" \
    --self-contained true \
    -p:UseAppHost=true \
    -p:Version="${package_version}" \
    -o "${publish_directory_path}"

package_deb "${assembly_name}"
package_portable "${assembly_name}"
