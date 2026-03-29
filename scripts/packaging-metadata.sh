#!/usr/bin/env bash

packaging_metadata_path=""
version_metadata_path=""

initialize_packaging_metadata() {
    local repo_root="$1"
    packaging_metadata_path="${repo_root}/packaging/release-metadata.xml"
    version_metadata_path="${repo_root}/Version.props"

    if [[ ! -f "${packaging_metadata_path}" ]]; then
        echo "Packaging metadata was not found at '${packaging_metadata_path}'." >&2
        exit 1
    fi

    if [[ ! -f "${version_metadata_path}" ]]; then
        echo "Version metadata was not found at '${version_metadata_path}'." >&2
        exit 1
    fi
}

get_xml_value() {
    local file_path="$1"
    local xpath="$2"

    if command -v xmllint >/dev/null 2>&1; then
        xmllint --xpath "string(${xpath})" "${file_path}" 2>/dev/null || true
        return
    fi

    local element_name="${xpath##*/}"
    sed -n "s:.*<${element_name}>\\(.*\\)</${element_name}>.*:\\1:p" "${file_path}" | head -n 1
}

get_packaging_metadata_value() {
    local xpath="$1"

    if [[ -z "${packaging_metadata_path}" ]]; then
        echo "Packaging metadata was not initialized." >&2
        exit 1
    fi

    get_xml_value "${packaging_metadata_path}" "${xpath}"
}

get_repo_version() {
    if [[ -z "${version_metadata_path}" ]]; then
        echo "Version metadata was not initialized." >&2
        exit 1
    fi

    local version_value
    version_value="$(get_xml_value "${version_metadata_path}" '//TokenMapVersion')"
    if [[ -z "${version_value}" ]]; then
        echo "Repo version was not found in '${version_metadata_path}'." >&2
        exit 1
    fi

    printf '%s' "${version_value}"
}
