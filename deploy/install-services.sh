#!/usr/bin/env bash
set -euo pipefail
release="$(realpath -- "${1:?Usage: install-services.sh RELEASE_DIRECTORY DOTNET_DIRECTORY}")"
dotnet_source="$(realpath -- "${2:?Pass the installed .NET10 runtime directory}")"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
[[ $EUID -eq 0 && -f "$release/platform/Platform.dll" && -f "$release/worker/Worker.dll" && -f "$dotnet_source/dotnet" ]] || exit 1
install -d -m 0755 /opt/tsr/releases /etc/tsr
for file in /var/lib/tsr/data/contest.db /var/lib/tsr/data/contest.db-wal /var/lib/tsr/data/contest.db-shm; do
  if [[ -f "$file" && ! -L "$file" ]]; then chgrp tsr-data "$file"; chmod 0660 "$file"; fi
done
release_id="$(date -u +%Y%m%dT%H%M%SZ)"
destination="/opt/tsr/releases/$release_id"
[[ ! -e "$destination" ]] || exit 1
cp -a "$release" "$destination"
chown -R root:root "$destination"
chmod -R a+rX "$destination"
if [[ ! -d /opt/tsr/dotnet ]]; then cp -a "$dotnet_source" /opt/tsr/dotnet; chown -R root:root /opt/tsr/dotnet; chmod -R a+rX /opt/tsr/dotnet; fi
if [[ -L /opt/tsr/current ]]; then readlink /opt/tsr/current > /opt/tsr/previous-release; fi
ln -sfn -- "$destination" /opt/tsr/current
printf '%s\n' 'TSR_DATA=/var/lib/tsr/data' 'TSR_REPOSITORY=/opt/tsr/current' 'TSR_SANDBOX_HELPER=/usr/local/libexec/tsr-sandbox' > /etc/tsr/contest.env
chown root:tsr-data /etc/tsr/contest.env
chmod 0640 /etc/tsr/contest.env
install -m 0644 "$script_dir/systemd/tsr-platform.service" "$script_dir/systemd/tsr-worker.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable tsr-platform.service
systemctl restart tsr-platform.service
echo TSR_PLATFORM_INSTALLED
