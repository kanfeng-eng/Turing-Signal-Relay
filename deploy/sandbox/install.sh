#!/usr/bin/env bash
set -euo pipefail
deploy_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository="$(cd -- "$deploy_dir/../.." && pwd)"
[[ $EUID -eq 0 ]] || { echo 'Run this project installer with sudo.' >&2; exit 1; }
nsjail_binary="${1:?Pass the built, pinned nsjail executable}"
groupadd -f tsr-data
id tsr-platform >/dev/null 2>&1 || useradd --system --home-dir /var/lib/tsr --shell /usr/sbin/nologin --gid tsr-data tsr-platform
id tsr-worker >/dev/null 2>&1 || useradd --system --home-dir /var/lib/tsr --shell /usr/sbin/nologin --gid tsr-data tsr-worker
install -d -m 0755 /opt/tsr/sandbox /usr/local/libexec
install -m 0755 "$nsjail_binary" /opt/tsr/sandbox/nsjail
install -m 0755 "$deploy_dir/tsr-sandbox.py" /usr/local/libexec/tsr-sandbox
install -m 0644 "$deploy_dir/run.policy" "$deploy_dir/compile.policy" /opt/tsr/sandbox/
install -d -m 0755 /opt/tsr/sandbox/sdk
cp -a "$repository/sdk/cpp/." /opt/tsr/sandbox/sdk/
chown -R root:root /opt/tsr/sandbox
chmod -R a-w /opt/tsr/sandbox/sdk
install -d -m 0755 /var/lib/tsr-sandbox /var/lib/tsr-sandbox/work /var/lib/tsr-sandbox/submissions /var/lib/tsr-sandbox/runs /var/lib/tsr-sandbox/compiling /var/lib/tsr-sandbox/compile-locks
install -d -m 2770 -o tsr-platform -g tsr-data /var/lib/tsr/data
printf '%s\n' 'tsr-worker ALL=(root) NOPASSWD: /usr/local/libexec/tsr-sandbox' > /etc/sudoers.d/tsr-worker
chmod 0440 /etc/sudoers.d/tsr-worker
visudo -cf /etc/sudoers.d/tsr-worker
for controller in memory pids cpu; do
  echo "+$controller" > /sys/fs/cgroup/cgroup.subtree_control
done
mkdir -p /sys/fs/cgroup/tsr
echo '+memory +pids +cpu' > /sys/fs/cgroup/tsr/cgroup.subtree_control
echo TSR_SANDBOX_INSTALLED
