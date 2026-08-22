from launch import LaunchDescription
from launch_ros.actions import Node


def generate_launch_description():

    ros_tcp_endpoint_node = Node(
        package='ros_tcp_endpoint',
        executable='default_server_endpoint',
        output='screen'
    )

    arviz_desktop_server_node = Node(
        package='arviz_desktop_server',
        executable='arviz_desktop_server',
        output='screen'
    )

    return LaunchDescription([
        ros_tcp_endpoint_node,
        arviz_desktop_server_node
    ])
