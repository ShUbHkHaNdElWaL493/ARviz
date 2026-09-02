cmake_minimum_required(VERSION 3.10.2)
project(NativeCV)

find_package(OpenCV REQUIRED)

set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR})
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR})

add_library(aruco_tracker SHARED src/aruco_tracker.cpp)
target_include_directories(aruco_tracker PRIVATE ${OpenCV_INCLUDE_DIRS} include)

if(ANDROID)
    target_link_libraries(aruco_tracker ${OpenCV_LIBS} log)
else()
    target_link_libraries(aruco_tracker ${OpenCV_LIBS})
endif()