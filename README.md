# ARviz

This project aims to visualize robots through Aruco markers using augmented reality.

---

## Prerequisites

- ARviz Server:
    - Ubuntu [Tested on 24.04]
    - ROS2 [Tested on Jazzy Jalisco]
    - rosdep
    - unzip
- ARviz Client:
    - Android 8+
    - Linux [Tested on Ubuntu 24.04]
        - Fuse
        - OpenCV

---

## ARviz Server Setup

1. Download the `arviz_ros_ws.zip` file from the latest release in the server system.
2. Unzip the server package.
    ```
    unzip -q arviz_ros2_ws.zip
    rm arviz_ros2_ws.zip
    ```
3. Move to the server workspace and resolve ROS2 dependencies.
    ```
    cd ROS2
    rosdep install --from-paths src --ignore-src -y --rosdistro $ROS_DISTRO
    ```
3. Build the workspace. The `--symlink-install` option just reduces the size of the build.
    ```
    colcon build --symlink-install
    ```
4. Launch the desktop server **after** the publishing nodes are active. This ensures that the subscribers for the topics have the same QoS.
    ```
    # Start the publishers before starting the desktop server
    # This is just an example. You can replace this with your robot state publisher.
    ros2 launch ur_description view_ur.launch.py ur_type:=ur5e

    # Once the publisher is set up, launch the desktop server.
    source install/setup.bash
    ros2 launch arviz_desktop_server arviz_desktop_server.launch.py
    ```

---

## ARviz Client Setup

### Android
1. Download the `ARviz.apk` file from the latest release in the server system.
2. In your security settings, allow the option for installing from unknown sources.
3. Install and run the application.

### Linux
1. Download the `ARviz.AppImage` file from the latest release in the server system.
2. Allow the application to run as an executable and run it.
    ```
    chmod +x ARviz.AppImage
    ./ARviz.AppImage
    ```

---

## ARviz Usage

1. Download the `aruco_marker.png` file from the latest release and either print it or present it on a screen.
2. Enter the IP address and Port of the server in the right drawer under `Connection Settings` of the client. The default port for the tcp server is `10000`. Click `Connect`.
3. Enter the ArUco marker size in the text box under AR settings. Default value for the marker size is `50`. You can also use it to scale the robot.
4. Once the connection is established, enter the `robot_description` topic name in the left drawer of the client under `RobotModel` and toggle `Visualize`.
5. You should see the robot spawned at the center of the grid world.
6. Once you are able to visualize the robot in the grid world, toggle `AR`. The camera feed should now be visible.
7. Once the ArUco marker is in the field of vision, the robot will be spawned at the ArUco marker.

---

## Contributions
If you want to contribute to this project, feel free to contact me.