# ARviz

This project aims to visualize robots using Aruco markers or QR codes in an AR environment.

---

## Prerequisites
- Docker

---

## Technological Stack

### 1. The Core Engine Layer (The Hub)

This is where everything connects. Unity will act as the master controller, rendering the 3D graphics, handling the UI, and calling the native tracking scripts.

* **Engine:** Unity 3D (Use a Long-Term Support version, preferably **2022.3 LTS** for maximum stability with robotics plugins).
* **Scripting Language:** **C#** (.NET).
* **Camera Input:** Unity’s built-in **`WebCamTexture`**. This cross-platform class directly accesses the smartphone's camera, grabs the frames, and allows extracting raw byte arrays to pass to the OpenCV plugin.
* **Rendering Pipeline:** **Universal Render Pipeline (URP)**. URP is highly optimized for mobile devices and provides excellent lighting/shadows for the robot meshes without draining the battery.

### 2. The Computer Vision Layer (The AR Tracker)

This layer calculates the camera's real-world position relative to an ArUco marker.

* **Library:** **OpenCV C++** (specifically using the `objdetect` module for ArUco in OpenCV 4.7+, or `opencv_contrib` for older versions).
* **Architecture:** A Custom Native Plugin written in **C++** with an exposed `extern "C"` function for Unity to use.
* **Data Flow Pipeline (Per Frame):**
1. Unity’s `WebCamTexture` gets a camera frame.
2. C# extracts the raw pixel data (`GetRawTextureData()`) and passes a pointer (`IntPtr`) to the C++ plugin.
3. C++ converts the raw bytes to a `cv::Mat`.
4. C++ runs `cv::aruco::detectMarkers` and `cv::solvePnP` to get the Translation (`tvec`) and Rotation (`rvec`).
5. C++ returns the pose back to C#, where Unity applies it to the virtual Camera's `Transform`.



### 3. The Robotics Networking Layer (The Nerves)

This layer handles the communication between the mobile device and the ROS master (running on the robot or a host PC).

* **Unity Package:** **Unity Robotics Hub - ROS-TCP-Connector** (Officially maintained by Unity).
* **ROS Node:** **ROS-TCP-Endpoint**. This python node runs on the ROS machine, serializing messages into JSON/BSON and streaming them over standard TCP/IP (Wi-Fi) to the phone.
* **Protocol:** TCP Socket Connection.
* **Required Subscriptions:**
* `/joint_states` (`sensor_msgs/JointState`): To get real-time joint angles.
* `/tf` (`tf2_msgs/TFMessage`): If the robot relies on dynamic frames outside of standard joint states.



### 4. Robot Modeling & Kinematics Layer (The Body)

This layer handles translating the ROS data into a visual 3D representation.

* **Asset Pipeline:** **Unity URDF Importer**.
* *Note:* By default, this tool is meant to be used in the Unity Editor before you build the app. The URDF, `.stl`, and `.dae` (Collada) files are imported into Unity on the PC, which automatically generates the exact TF tree structure using Unity GameObjects.


* **Forward Kinematics (FK):**
* The imported URDF creates a parent-child hierarchy of `Transforms` (links) connected by `ArticulationBody` components (joints).
* There will be a C# script that listens to the `/joint_states` message, parses the names and positions (in radians), converts them to degrees, and applies them to the local rotations (Quaternions) of the respective joint GameObjects.



### 5. The Cross-Platform Build Pipeline

How compilation will be done for this stack into working apps for Android.

* **C++ Compilation:** **CMake** and the **Android NDK** will be used to compile the OpenCV code into a shared library (`.so` file) targeting the `ARM64` architecture.
* **Unity Placement:** The `.so` file will be placed in `Assets/Plugins/Android`.
* **Final Output:** Unity uses the Android SDK to bundle the C#, the `.so` file, and 3D assets into a native `.apk` or `.aab` file.

---

## Development
The development of this project consists of the following phases:
1. Creating extractors for robot_description and joint_states.
2. Converting the robot_description into 3-D models.
3. Testing the 3-D models on test interface.
4. Analyzing how AR shows the models in the real world.
5. Creating the Android app for accessing the service.
This is just a brief overview of how I am planning to develop the project.

---

## Progress
The following things have been done:

---

## Contributions
If you want to contribute to this project, feel free to mail me.