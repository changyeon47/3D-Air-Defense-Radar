from setuptools import setup
import os
from glob import glob

package_name = 'air_defense_sim'

setup(
    name=package_name,
    version='1.0.0',
    packages=[package_name],
    data_files=[
        ('share/ament_index/resource_index/packages',
            ['resource/' + package_name]),
        ('share/' + package_name, ['package.xml']),
        (os.path.join('share', package_name, 'worlds'),
            glob('worlds/*.world')),
        (os.path.join('share', package_name, 'launch'),
            glob('launch/*.py')),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    entry_points={
        'console_scripts': [
            'lidar_processor = air_defense_sim.lidar_processor_node:main',
            'target_mover    = air_defense_sim.target_mover_node:main',
        ],
    },
)
