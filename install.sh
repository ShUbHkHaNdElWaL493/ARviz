#!/bin/bash

cd -- "$( dirname -- "${BASH_SOURCE[0]}" )"

echo "[INSTALL.SH] Building ArUco tracker shared libraries for Unity."
docker build --output type=local,dest=./UnityClient/Assets/Plugins ./NativeCV/

echo "[INSTALL.SH] Building ROS2 workspace"
cd ROS2
colcon build --symlink-install
cd ..