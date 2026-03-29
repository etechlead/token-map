#!/usr/bin/env bash

set -euo pipefail

script_path="${BASH_SOURCE[0]}"

if command -v readlink >/dev/null 2>&1; then
    resolved_script_path="$(readlink -f "${script_path}" 2>/dev/null || true)"
    if [[ -n "${resolved_script_path}" ]]; then
        script_path="${resolved_script_path}"
    fi
fi

bundle_root="$(cd "$(dirname "${script_path}")" && pwd)"
launcher_path="${bundle_root}/tokenmap"
icon_path="${bundle_root}/tokenmap.svg"
desktop_directory_path="${XDG_DATA_HOME:-${HOME}/.local/share}/applications"
desktop_entry_path="${desktop_directory_path}/tokenmap-portable.desktop"

escape_desktop_exec_argument() {
    local value="$1"

    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    value="${value//%/%%}"

    printf '%s' "${value}"
}

if [[ ! -x "${launcher_path}" ]]; then
    echo "Portable launcher was not found at '${launcher_path}'." >&2
    exit 1
fi

if [[ ! -f "${icon_path}" ]]; then
    echo "Portable icon was not found at '${icon_path}'." >&2
    exit 1
fi

launcher_exec_argument="$(escape_desktop_exec_argument "${launcher_path}")"

mkdir -p "${desktop_directory_path}"

cat > "${desktop_entry_path}" <<EOF
[Desktop Entry]
Name=TokenMap
Comment=Local source-tree analysis
Exec="${launcher_exec_argument}"
Icon=${icon_path}
StartupWMClass=tokenmap
Path=${bundle_root}
Terminal=false
Type=Application
Categories=Development;
Keywords=token;treemap;code;analysis;
EOF

chmod 0644 "${desktop_entry_path}"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "${desktop_directory_path}" >/dev/null 2>&1 || true
fi

echo "Desktop entry installed at: ${desktop_entry_path}"
echo "If you move this portable folder, rerun this script to refresh the launcher path."
