cmake_minimum_required(VERSION 3.8)
project(arviz_desktop_server)

if(CMAKE_COMPILER_IS_GNUCXX OR CMAKE_CXX_COMPILER_ID MATCHES "Clang")
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

# find dependencies
find_package(ament_cmake REQUIRED)
find_package(ament_index_cpp REQUIRED)
find_package(assimp REQUIRED)
find_package(rclcpp REQUIRED)

include_directories(include)

add_executable(arviz_desktop_server src/arviz_desktop_server.cpp)
ament_target_dependencies(arviz_desktop_server rclcpp ament_index_cpp)
target_link_libraries(arviz_desktop_server assimp pthread)

install(TARGETS
  arviz_desktop_server
  DESTINATION lib/${PROJECT_NAME}
)

if(BUILD_TESTING)
  find_package(ament_lint_auto REQUIRED)
  # the following line skips the linter which checks for copyrights
  # comment the line when a copyright and license is added to all source files
  set(ament_cmake_copyright_FOUND TRUE)
  # the following line skips cpplint (only works in a git repo)
  # comment the line when this package is in a git repo and when
  # a copyright and license is added to all source files
  set(ament_cmake_cpplint_FOUND TRUE)
  ament_lint_auto_find_test_dependencies()
endif()

ament_package()
