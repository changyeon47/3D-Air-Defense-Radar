import os
from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.actions import ExecuteProcess
from launch_ros.actions import Node


def generate_launch_description():
    world = os.path.join(
        get_package_share_directory('air_defense_sim'),
        'worlds', 'air_defense.world'
    )

    return LaunchDescription([
        # Gazebo Harmonic 실행
        ExecuteProcess(
            cmd=['gz', 'sim', '--verbose', world],
            output='screen',
            additional_env={'LIBGL_ALWAYS_SOFTWARE': '1'},
        ),

        # LiDAR 토픽 브릿지: gz → ROS2
        # gz gpu_lidar → /lidar/points (PointCloud2)
        Node(
            package='ros_gz_bridge',
            executable='parameter_bridge',
            name='lidar_bridge',
            arguments=[
                '/lidar/points/points@sensor_msgs/msg/PointCloud2[gz.msgs.PointCloudPacked'
            ],
            remappings=[
                ('/lidar/points/points', '/lidar/points')
            ],
            output='screen',
        ),

        # 타겟 이동 노드 (드론 스폰 + 위치 업데이트)
        Node(
            package='air_defense_sim',
            executable='target_mover',
            name='target_mover',
            output='screen',
        ),

        # LiDAR 처리 + WebSocket 서버 노드
        Node(
            package='air_defense_sim',
            executable='lidar_processor',
            name='lidar_processor',
            output='screen',
        ),
    ])
