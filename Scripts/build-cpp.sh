#!/bin/bash

ANDROID_NDK_PATH="$HOME/Unity/Hub/Editor/6000.5.9f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK"
OPENCV_ANDROID_DIR="$HOME/local/OpenCV-android-sdk/sdk/native/jni"

cd -- "$( dirname -- "${BASH_SOURCE[0]}" )/../NativeCV"

mkdir -p build_android
cmake -B build_android \
    -DCMAKE_TOOLCHAIN_FILE="${ANDROID_NDK_PATH}/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI="arm64-v8a" \
    -DANDROID_PLATFORM=latest \
    -DOpenCV_DIR="${OPENCV_ANDROID_DIR}"
cmake --build build_android

mkdir -p build_linux
cmake -B build_linux
cmake --build build_linux

cd ..
mkdir -p UnityClient/Assets/Plugins/Android/arm64-v8a
mkdir -p UnityClient/Assets/Plugins/Linux

cp NativeCV/build_android/libaruco_tracker.so UnityClient/Assets/Plugins/Android/arm64-v8a
cp NativeCV/build_linux/libaruco_tracker.so UnityClient/Assets/Plugins/Linux