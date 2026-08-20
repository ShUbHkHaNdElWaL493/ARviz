cmake_minimum_required(VERSION 3.10.2)
project(NativeCV)

find_package(OpenCV REQUIRED)

add_library(aruco_tracker SHARED src/aruco_tracker.cpp)
target_include_directories(aruco_tracker PRIVATE ${OpenCV_INCLUDE_DIRS} include)
target_link_libraries(aruco_tracker ${OpenCV_LIBS} log)