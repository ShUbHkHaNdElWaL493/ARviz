FROM ubuntu:24.04 AS builder

SHELL ["/bin/bash", "-c"]

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    libopencv-dev \
    wget \
    zip \
    && rm -rf /var/lib/apt/lists/*

RUN wget -q https://github.com/opencv/opencv/releases/download/5.0.0/opencv-5.0.0-android-sdk.zip \
    && unzip -q opencv-5.0.0-android-sdk.zip \
    && rm opencv-5.0.0-android-sdk.zip

RUN wget -q https://dl.google.com/android/repository/android-ndk-r27d-linux.zip \
    && unzip -q android-ndk-r27d-linux.zip \
    && rm android-ndk-r27d-linux.zip

WORKDIR /NativeCV
COPY . .

RUN mkdir -p build/Android/arm64-v8a \
    && cmake -B build/Android/arm64-v8a \
    -DCMAKE_TOOLCHAIN_FILE="/android-ndk-r27d/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI="arm64-v8a" \
    -DANDROID_PLATFORM=latest \
    -DOpenCV_DIR="/OpenCV-android-sdk/sdk/native/jni" \
    && cmake --build build/Android/arm64-v8a

RUN mkdir -p build/Android/armeabi-v7a \
    && cmake -B build/Android/armeabi-v7a \
    -DCMAKE_TOOLCHAIN_FILE="/android-ndk-r27d/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI="armeabi-v7a" \
    -DANDROID_PLATFORM=latest \
    -DOpenCV_DIR="/OpenCV-android-sdk/sdk/native/jni" \
    && cmake --build build/Android/armeabi-v7a

RUN mkdir -p build/Android/x86_64 \
    && cmake -B build/Android/x86_64 \
    -DCMAKE_TOOLCHAIN_FILE="/android-ndk-r27d/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI="x86_64" \
    -DANDROID_PLATFORM=latest \
    -DOpenCV_DIR="/OpenCV-android-sdk/sdk/native/jni" \
    && cmake --build build/Android/x86_64

RUN mkdir -p build/Linux \
    && cmake -B build/Linux \
    && cmake --build build/Linux

FROM scratch AS exporter
COPY --from=builder /NativeCV/build/Android/arm64-v8a/libaruco_tracker.so /Android/arm64-v8a/
COPY --from=builder /NativeCV/build/Android/armeabi-v7a/libaruco_tracker.so /Android/armeabi-v7a/
COPY --from=builder /NativeCV/build/Android/x86_64/libaruco_tracker.so /Android/x86_64/
COPY --from=builder /NativeCV/build/Linux/libaruco_tracker.so /Linux/