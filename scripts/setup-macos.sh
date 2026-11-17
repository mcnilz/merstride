#!/bin/bash
set -euo pipefail
test "$(uname -m)" = arm64
test "$(sw_vers -productVersion | cut -d. -f1)" -ge 15
test -d /Applications/Xcode_16.4.app
sudo -n xcode-select --switch /Applications/Xcode_16.4.app/Contents/Developer
xcrun --find clang
xcrun --find dsymutil
command -v pwsh
command -v python3
python3 -c 'import sys; assert sys.version_info >= (3, 12), "Python 3.12+ is required"'
command -v bash
node -e 'const [a,b]=process.versions.node.split(".").map(Number); if(a<22 || (a===22 && b<12)) process.exit(1)'
test -x '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
mkdir -p artifacts
{
    sw_vers
    uname -m
    xcodebuild -version
    xcrun clang --version
    pwsh -NoProfile -Command '$PSVersionTable.PSVersion.ToString()'
    python3 --version
    bash --version
    node --version
    printf 'ImageOS=%s\nImageVersion=%s\n' "${ImageOS:-unavailable}" "${ImageVersion:-unavailable}"
} > artifacts/macos-toolchain.txt
