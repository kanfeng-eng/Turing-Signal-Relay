#!/usr/bin/env bash
set -euo pipefail
output="${1:?Usage: build-nsjail.sh NEW_BUILD_DIRECTORY}"
[[ ! -e "$output" ]] || { echo 'Use a new build directory.' >&2; exit 1; }
mkdir -p "$output"
output="$(realpath "$output")"
nsjail_commit=d8415bc8eea43cdd413daba25af63127019a1189
kafel_commit=76d0f41bf3eb5c4008713d64b9767b461a9129a3
curl --fail --location --retry 2 "https://codeload.github.com/google/nsjail/tar.gz/$nsjail_commit" -o "$output/nsjail.tar.gz"
curl --fail --location --retry 2 "https://codeload.github.com/google/kafel/tar.gz/$kafel_commit" -o "$output/kafel.tar.gz"
(cd "$output" && printf '%s\n' 'b051697310426332b2b60706ce99c0009dd8f77413fe637e35b8bedbe12281fb  nsjail.tar.gz' 'b9a99b91af7abc84664803709072ee7f6a787dd3f39686484503154d93ef8f33  kafel.tar.gz' | sha256sum -c -)
tar -xzf "$output/nsjail.tar.gz" -C "$output"
tar -xzf "$output/kafel.tar.gz" -C "$output"
cp -a "$output/kafel-$kafel_commit/." "$output/nsjail-$nsjail_commit/kafel/"
make -j2 -C "$output/nsjail-$nsjail_commit"
dpkg-query -W g++ libprotobuf-dev libnl-route-3-dev bison flex make > "$output/host-packages.txt"
printf '%s\n' "$output/nsjail-$nsjail_commit/nsjail"
