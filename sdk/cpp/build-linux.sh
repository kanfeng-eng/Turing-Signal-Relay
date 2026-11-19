#!/usr/bin/env bash
set -euo pipefail
sdk_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
output="${1:?Usage: build-linux.sh NEW_OUTPUT_DIRECTORY [--faults]}"
if [[ -e "$output" ]]; then echo 'Use a new output directory.' >&2; exit 1; fi
mkdir -p -- "$output"
compiler="${CXX:-g++}"
for name in courier transporter; do
  "$compiler" -std=c++17 -O2 -static-libgcc -static-libstdc++ "$sdk_dir/examples/$name.cpp" -o "$output/$name"
done
if [[ "${2:-}" == '--faults' ]]; then
  names=(malformed oversize crash timeout late-once memory stderr future-tick cpu)
  for mode in "${!names[@]}"; do
    "$compiler" -std=c++17 -O2 -static-libgcc -static-libstdc++ -DFAULT_MODE="$mode" "$sdk_dir/tests/faults.cpp" -o "$output/${names[$mode]}"
  done
fi
echo SAMPLE_AI_BUILD_PASSED
