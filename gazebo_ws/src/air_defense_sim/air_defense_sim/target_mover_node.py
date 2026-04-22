#!/usr/bin/env python3
"""
target_mover_node.py  (Gazebo Harmonic / ROS2 Jazzy)

- gz service CLI로 드론 스폰 + 위치 업데이트
- 스폰: /world/air_defense/create
- 이동: /world/air_defense/set_pose  (백그라운드 스레드, 5Hz)
"""

import math
import random
import subprocess
import threading
import rclpy
from rclpy.node import Node

WORLD        = 'air_defense'
N_TARGETS    = 3
TARGET_RADIUS = 1.5
ARENA_SIZE   = 80.0
ALT_MIN      = 20.0
ALT_MAX      = 60.0
SPEED        = 8.0
UPDATE_HZ    = 20.0
POSE_HZ      = 5       # gz service 호출 주파수 (UPDATE_HZ의 분수)

_SPHERE_SDF = """\
<sdf version="1.8">
  <model name="{name}">
    <pose>{x:.3f} {y:.3f} {z:.3f} 0 0 0</pose>
    <static>false</static>
    <link name="link">
      <visual name="vis">
        <geometry><sphere><radius>{r}</radius></sphere></geometry>
        <material>
          <ambient>1.0 0.1 0.1 1</ambient>
          <diffuse>1.0 0.1 0.1 1</diffuse>
          <emissive>0.8 0.0 0.0 1</emissive>
        </material>
      </visual>
      <collision name="col">
        <geometry><sphere><radius>{r}</radius></sphere></geometry>
      </collision>
      <inertial><mass>1</mass></inertial>
    </link>
  </model>
</sdf>"""


def _gz_run(args: list, timeout: float = 3.0):
    try:
        subprocess.run(args, capture_output=True, timeout=timeout)
    except Exception:
        pass


def _gz_spawn(name: str, x: float, y: float, z: float):
    sdf = _SPHERE_SDF.format(name=name, x=x, y=y, z=z, r=TARGET_RADIUS)
    req = f'sdf: "{sdf.replace(chr(10), " ").replace(chr(34), chr(39))}"'
    _gz_run([
        'gz', 'service',
        '-s', f'/world/{WORLD}/create',
        '--reqtype', 'gz.msgs.EntityFactory',
        '--reptype', 'gz.msgs.Boolean',
        '--timeout', '5000',
        '--req', req,
    ], timeout=6.0)


def _gz_set_pose(name: str, x: float, y: float, z: float):
    req = (f'name: "{name}" '
           f'position: {{x: {x:.3f} y: {y:.3f} z: {z:.3f}}} '
           f'orientation: {{w: 1.0}}')
    threading.Thread(
        target=_gz_run,
        args=([[
            'gz', 'service',
            '-s', f'/world/{WORLD}/set_pose',
            '--reqtype', 'gz.msgs.Pose',
            '--reptype', 'gz.msgs.Boolean',
            '--timeout', '300',
            '--req', req,
        ], 1.0],),
        daemon=True,
    ).start()


class _Target:
    def __init__(self, tid: int):
        self.name = f'drone_target_{tid}'
        self.x = self.y = self.z = 0.0
        self.vx = self.vy = self.vz = 0.0
        self._respawn()

    def _respawn(self):
        angle = random.uniform(0, 2 * math.pi)
        dist  = random.uniform(ARENA_SIZE * 0.55, ARENA_SIZE)
        self.x = dist * math.cos(angle)
        self.y = dist * math.sin(angle)
        self.z = random.uniform(ALT_MIN, ALT_MAX)
        to_sensor = math.atan2(-self.y, -self.x)
        spd = random.uniform(SPEED * 0.7, SPEED * 1.4)
        self.vx = spd * math.cos(to_sensor) + random.uniform(-2, 2)
        self.vy = spd * math.sin(to_sensor) + random.uniform(-2, 2)
        self.vz = random.uniform(-1.5, 1.5)

    def update(self, dt: float):
        self.x += self.vx * dt
        self.y += self.vy * dt
        self.z += self.vz * dt
        if self.z < ALT_MIN:
            self.z, self.vz = ALT_MIN, abs(self.vz)
        elif self.z > ALT_MAX:
            self.z, self.vz = ALT_MAX, -abs(self.vz)
        if math.hypot(self.x, self.y) < 8.0 or math.hypot(self.x, self.y) > ARENA_SIZE * 1.6:
            self._respawn()


class TargetMoverNode(Node):
    def __init__(self):
        super().__init__('target_mover')
        self._targets  = [_Target(i) for i in range(N_TARGETS)]
        self._last_t   = self.get_clock().now()
        self._tick_cnt = 0

        self.get_logger().info('Gazebo 준비 대기 (5초)...')
        import time; time.sleep(5.0)

        self.get_logger().info('드론 스폰 중...')
        for t in self._targets:
            threading.Thread(
                target=_gz_spawn,
                args=(t.name, t.x, t.y, t.z),
                daemon=True,
            ).start()

        import time; time.sleep(2.0)
        self.get_logger().info('스폰 완료 — 이동 시작')
        self.create_timer(1.0 / UPDATE_HZ, self._tick)

    def _tick(self):
        now = self.get_clock().now()
        dt  = (now - self._last_t).nanoseconds * 1e-9
        self._last_t = now
        self._tick_cnt += 1

        for t in self._targets:
            t.update(dt)

        # gz service는 POSE_HZ 간격으로만 호출
        if self._tick_cnt % max(1, int(UPDATE_HZ / POSE_HZ)) == 0:
            for t in self._targets:
                _gz_set_pose(t.name, t.x, t.y, t.z)


def main():
    rclpy.init()
    node = TargetMoverNode()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()
