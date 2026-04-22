#!/usr/bin/env python3
"""
lidar_processor_node.py
Gazebo /lidar/points (PointCloud2) 수신 → 3D 배경 제거 → 클러스터링 → WebSocket → Unity

좌표 변환:
  Gazebo ENU: x=East, y=North, z=Up
  Unity:      x=East, y=Up,    z=North (North = Gazebo y)
  → Unity_x = Gazebo_x, Unity_y = Gazebo_z, Unity_z = Gazebo_y
"""

import asyncio
import json
import math
import threading

import numpy as np
import rclpy
from rclpy.node import Node
from sensor_msgs.msg import PointCloud2
from geometry_msgs.msg import PoseArray, Pose
import sensor_msgs_py.point_cloud2 as pc2
import websockets

# ── 파라미터 ──────────────────────────────────────────────────────────
WS_PORT         = 8081
SEND_HZ         = 10

BG_LEARN_FRAMES = 25
BG_VOXEL_SIZE   = 1.0     # m

CLUSTER_DIST    = 4.0     # m
MIN_CLUSTER_PTS = 3

# LiDAR 센서 높이 오프셋 (world 파일에서 lidar_link z=2m)
SENSOR_Z_OFFSET = 2.0

# ── 공유 상태 ─────────────────────────────────────────────────────────
_lock        = threading.Lock()
_targets_out = []
_clients     = set()

_bg_voxels   = set()
_bg_count    = 0
_bg_learning = True


# ── 3D 처리 함수 ──────────────────────────────────────────────────────

def _to_voxel(x, y, z):
    s = BG_VOXEL_SIZE
    return (int(x / s), int(y / s), int(z / s))


def _learn_bg(points):
    global _bg_count, _bg_learning
    for (x, y, z) in points:
        vx, vy, vz = _to_voxel(x, y, z)
        for dx in (-1, 0, 1):
            for dy2 in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    _bg_voxels.add((vx+dx, vy+dy2, vz+dz))
    _bg_count += 1
    rem = BG_LEARN_FRAMES - _bg_count
    if rem > 0:
        print(f"[배경 학습] {_bg_count}/{BG_LEARN_FRAMES} ({rem}회 남음)")
    else:
        _bg_learning = False
        print(f"[배경 학습 완료] 복셀 {len(_bg_voxels)}개 → 탐지 시작")


def _filter_moving(points):
    return [(x, y, z) for (x, y, z) in points if _to_voxel(x, y, z) not in _bg_voxels]


def _cluster(points):
    if not points:
        return []
    pts   = np.array(points, dtype=np.float32)
    n     = len(pts)
    used  = np.zeros(n, dtype=bool)

    if n > 1:
        diff  = pts[:, None, :] - pts[None, :, :]
        dists = np.sqrt((diff ** 2).sum(axis=2))
    else:
        dists = np.zeros((1, 1), dtype=np.float32)

    centroids = []
    for i in range(n):
        if used[i]:
            continue
        neighbors = np.where(~used & (dists[i] < CLUSTER_DIST))[0]
        if len(neighbors) >= MIN_CLUSTER_PTS:
            used[neighbors] = True
            centroids.append(pts[neighbors].mean(axis=0).tolist())
    return centroids


# ── ROS2 노드 ─────────────────────────────────────────────────────────

class LiDARProcessorNode(Node):
    def __init__(self):
        super().__init__('lidar_processor')
        self.sub = self.create_subscription(
            PointCloud2, '/lidar/points', self._on_scan, 10)
        self.pub = self.create_publisher(PoseArray, '/detected_targets', 10)
        self.get_logger().info(f'[LiDAR] /lidar/points 구독 시작 → ws://0.0.0.0:{WS_PORT}, /detected_targets 퍼블리시')

    def _on_scan(self, msg: PointCloud2):
        global _targets_out

        # PointCloud2 → (x, y, z) 리스트 (센서 로컬 프레임)
        raw = list(pc2.read_points(msg, field_names=('x', 'y', 'z'), skip_nans=True))
        if not raw:
            return

        # 센서 로컬 → 월드 좌표 (센서가 z=SENSOR_Z_OFFSET에 고정)
        # Gazebo 로컬 = 월드 (센서 회전 없음, 위치만 오프셋)
        # Gazebo → Unity 좌표 변환 포함:
        #   Unity_x = Gazebo_x, Unity_y = Gazebo_z + offset, Unity_z = Gazebo_y
        unity_pts = []
        for (lx, ly, lz) in raw:
            wx = float(lx)
            wy = float(lz) + SENSOR_Z_OFFSET   # Gazebo z(Up) → Unity y
            wz = float(ly)                       # Gazebo y(North) → Unity z
            unity_pts.append((wx, wy, wz))

        if _bg_learning:
            _learn_bg(unity_pts)
            with _lock:
                _targets_out = []
            return

        moving = _filter_moving(unity_pts)
        centroids = _cluster(moving)

        new_out = []
        for idx, (cx, cy, cz) in enumerate(centroids):
            new_out.append({
                'id': idx,
                'x': round(cx, 2),
                'y': round(cy, 2),
                'z': round(cz, 2),
            })

        with _lock:
            _targets_out = new_out

        # /detected_targets PoseArray 퍼블리시
        pa = PoseArray()
        pa.header.stamp = self.get_clock().now().to_msg()
        pa.header.frame_id = 'map'
        for t in new_out:
            p = Pose()
            p.position.x = float(t['x'])
            p.position.y = float(t['y'])
            p.position.z = float(t['z'])
            p.orientation.w = 1.0
            pa.poses.append(p)
        self.pub.publish(pa)

        if new_out:
            for t in new_out:
                print(f"  [탐지] 타겟{t['id']}: "
                      f"({t['x']:.1f}, {t['y']:.1f}, {t['z']:.1f})m  "
                      f"히트={len(moving)}pts")


# ── WebSocket 서버 ─────────────────────────────────────────────────────

async def _ws_handler(websocket):
    _clients.add(websocket)
    print(f"[WS] Unity 접속: {websocket.remote_address}")
    try:
        async for _ in websocket:
            pass
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        _clients.discard(websocket)
        print(f"[WS] 연결 해제: {websocket.remote_address}")


async def _broadcast_loop():
    interval = 1.0 / SEND_HZ
    while True:
        await asyncio.sleep(interval)
        if not _clients:
            continue
        with _lock:
            payload = json.dumps({'targets': list(_targets_out)})
        dead = set()
        for ws in list(_clients):
            try:
                await ws.send(payload)
            except Exception:
                dead.add(ws)
        _clients.difference_update(dead)


def _run_ws_server():
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)

    async def _serve():
        print(f"[WS] ws://0.0.0.0:{WS_PORT} 대기 중...")
        async with websockets.serve(_ws_handler, '0.0.0.0', WS_PORT):
            await _broadcast_loop()

    loop.run_until_complete(_serve())


# ── 엔트리포인트 ──────────────────────────────────────────────────────

def main():
    rclpy.init()
    node = LiDARProcessorNode()

    ws_thread = threading.Thread(target=_run_ws_server, daemon=True)
    ws_thread.start()

    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()
