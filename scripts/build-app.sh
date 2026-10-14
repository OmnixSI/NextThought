#!/bin/bash
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
build_root="$project_root/.build"
runtime_build="$build_root/whisper-runtime"
app_path="$project_root/dist/InterviewHacker.app"
archive_path="$project_root/dist/InterviewHacker-macOS.zip"
clang_cache="$build_root/clang-module-cache"
swift_cache="$build_root/swift-module-cache"

mkdir -p "$clang_cache" "$swift_cache" "$project_root/dist"
export CLANG_MODULE_CACHE_PATH="$clang_cache"
export SWIFT_MODULE_CACHE_PATH="$swift_cache"

cd "$project_root"
swift build --disable-sandbox -c release --scratch-path "$build_root/swift"
binary_path="$(swift build --disable-sandbox --show-bin-path -c release --scratch-path "$build_root/swift")/InterviewHacker"

cmake -S Vendor/whisper.cpp -B "$runtime_build" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 \
    -DBUILD_SHARED_LIBS=OFF \
    -DWHISPER_BUILD_IS_DEV=OFF \
    -DWHISPER_BUILD_TESTS=OFF \
    -DWHISPER_BUILD_EXAMPLES=ON \
    -DWHISPER_BUILD_SERVER=ON \
    -DGGML_NATIVE=OFF \
    -DGGML_METAL=ON \
    -DGGML_METAL_EMBED_LIBRARY=ON \
    -DGGML_ACCELERATE=ON \
    -DGGML_BLAS=ON \
    -DGGML_BLAS_VENDOR=Apple \
    -DGGML_BACKEND_DL=OFF
cmake --build "$runtime_build" --target whisper-server --parallel "${INTERVIEWHACKER_BUILD_JOBS:-4}"

if [[ ! -x "$binary_path" ]]; then
    echo "Could not find the compiled InterviewHacker executable." >&2
    exit 1
fi

runtime_path="$runtime_build/bin/whisper-server"
if [[ ! -x "$runtime_path" ]]; then
    echo "Could not find the compiled Whisper runtime." >&2
    exit 1
fi

rm -rf "$app_path"
mkdir -p "$app_path/Contents/MacOS" "$app_path/Contents/Resources" "$app_path/Contents/Helpers"
cp "$binary_path" "$app_path/Contents/MacOS/InterviewHacker"
cp "$runtime_path" "$app_path/Contents/Helpers/whisper-server"
cp "$project_root/Resources/Info.plist" "$app_path/Contents/Info.plist"
cp "$project_root/Vendor/whisper.cpp/LICENSE" "$app_path/Contents/Resources/WhisperCpp-LICENSE.txt"
cp "$project_root/Resources/ThirdPartyNotices.txt" "$app_path/Contents/Resources/ThirdPartyNotices.txt"
printf 'APPL????' > "$app_path/Contents/PkgInfo"

"$project_root/scripts/sign-app.sh" "$app_path"

codesign --verify --strict "$app_path/Contents/Helpers/whisper-server"
codesign --verify --deep --strict "$app_path"
lipo "$app_path/Contents/MacOS/InterviewHacker" -verify_arch arm64
lipo "$app_path/Contents/Helpers/whisper-server" -verify_arch arm64

rm -f "$archive_path"
ditto -c -k --sequesterRsrc --keepParent "$app_path" "$archive_path"

echo "Built arm64 macOS 14+ app: $app_path"
echo "Created archive: $archive_path"
echo "Distribution: local development build; not notarized."
