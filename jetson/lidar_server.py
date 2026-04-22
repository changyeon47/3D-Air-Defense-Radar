#!/usr/bin/env python3
"""
lidar_server.py
YDLIDAR TG30 → 움직이는 물체만 감지 → WebSocket으로 Unity에 전송
Jetson Orin Nano에서 실행

설치:
  pip3 install websockets
  # YDLIDAR SDK 설치:
  # https://github.com/YDLIDAR/YDLidar-SDK
  # cd YDLidar-SDK && pip3 install .
"""

import asyncio
import json
import math
import threading
import time
import websockets

try:
    import ydlidar
    YDLIDAR_AVAILABLE = True
except ImportError:
    print("[경고] ydlidar 모듈 없음 → 시뮬레이션 모드")
    YDLIDAR_AVAILABLE = False

# ── 기본 설정 ──────────────────────────────────────────────────────────
PORT            = 8081
LIDAR_PORT      = "/dev/ttyUSB0"
BAUD_RATE       = 512000
MIN_RANGE       = 0.15
MAX_RANGE       = 8.0
MIN_TARGET_DIST = 1.5
CLUSTER_DIST    = 0.35
MIN_CLUSTER_PTS = 3
UNITY_SCALE     = 4.0
UNITY_HEIGHT    = 12.0
SEND_HZ         = 10

# ── 배경 제거 설정 ─────────────────────────────────────────────────────
BG_LEARN_FRAMES = 50     # 시작 시 배경 학습에 사용할 스캔 수 (~5초)
BG_CELL_SIZE    = 0.10   # 배경 격자 크기(m) — 이 거리 이내는 정적으로 간주
# ──────────────────────────────────────────────────────────────────────

_lock        = threading.Lock()
_targets     = []
_clients     = set()

# 배경 상태
_bg_cells    = set()   # 정적 배경 격자 셀 집합
_bg_count    = 0       # 학습된 스캔 수
_bg_learning = True    # True: 학습 중 / False: 감지 중


def _to_cell(x: float, y: float):
    """좌표 → 격자 셀 인덱스"""
    return (int(x / BG_CELL_SIZE), int(y / BG_CELL_SIZE))


def learn_background(points: list):
    """현재 스캔의 모든 점을 배경 격자에 등록"""
    global _bg_count, _bg_learning

    for (x, y) in points:
        cx, cy = _to_cell(x, y)
        # 격자 셀 + 주변 1칸 여유를 배경으로 등록 (센서 노이즈 흡수)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                _bg_cells.add((cx + dx, cy + dy))

    _bg_count += 1
    remaining = BG_LEARN_FRAMES - _bg_count
    if remaining > 0:
        print(f"[배경 학습] {_bg_count}/{BG_LEARN_FRAMES} — 움직이지 마세요... ({remaining}회 남음)")
    else:
        _bg_learning = False
        print(f"[배경 학습 완료] 격자 셀 {len(_bg_cells)}개 등록 → 움직이는 물체 감지 시작")


def filter_moving_points(points: list) -> list:
    """배경에 없는 점(움직이는 물체)만 반환"""
    moving = []
    for (x, y) in points:
        cell = _to_cell(x, y)
        if cell not in _bg_cells:
            moving.append((x, y))
    return moving


# ── 거리 기반 클러스터링 ────────────────────────────────────────────────
def cluster_points(points: list) -> list:
    if not points:
        return []

    used = [False] * len(points)
    centroids = []

    for i, (px, py) in enumerate(points):
        if used[i]:
            continue
        group = [(px, py)]
        used[i] = True

        for j, (qx, qy) in enumerate(points):
            if used[j]:
                continue
            if math.hypot(px - qx, py - qy) < CLUSTER_DIST:
                group.append((qx, qy))
                used[j] = True

        if len(group) >= MIN_CLUSTER_PTS:
            cx = sum(p[0] for p in group) / len(group)
            cy = sum(p[1] for p in group) / len(group)
            centroids.append((cx, cy))

    return centroids


# ── LiDAR 읽기 스레드 ──────────────────────────────────────────────────
def lidar_thread():
    global _targets

    if not YDLIDAR_AVAILABLE:
        _run_simulation()
        return

    laser = ydlidar.CYdLidar()
    laser.setlidaropt(ydlidar.LidarPropSerialPort,     LIDAR_PORT)
    laser.setlidaropt(ydlidar.LidarPropSerialBaudrate, BAUD_RATE)
    laser.setlidaropt(ydlidar.LidarPropLidarType,      ydlidar.TYPE_TRIANGLE)
    laser.setlidaropt(ydlidar.LidarPropDeviceType,     ydlidar.YDLIDAR_TYPE_SERIAL)
    laser.setlidaropt(ydlidar.LidarPropScanFrequency,  10.0)
    laser.setlidaropt(ydlidar.LidarPropSampleRate,     5)
    laser.setlidaropt(ydlidar.LidarPropMaxRange,       MAX_RANGE)
    laser.setlidaropt(ydlidar.LidarPropMinRange,       MIN_RANGE)
    laser.setlidaropt(ydlidar.LidarPropMaxAngle,        50.0)
    laser.setlidaropt(ydlidar.LidarPropMinAngle,      -50.0)
    laser.setlidaropt(ydlidar.LidarPropIntenstiy,      False)

    if not laser.initialize():
        print("[LiDAR] 초기화 실패 — 포트/권한 확인: sudo chmod 666 /dev/ttyUSB0")
        return
    if not laser.turnOn():
        print("[LiDAR] 구동 실패")
        return

    print(f"[LiDAR] TG30 시작 — 배경 학습 중... ({LIDAR_PORT})")
    scan = ydlidar.LaserScan()

    while True:
        if not laser.doProcessSimple(scan):
            continue

        # 유효 포인트 수집
        points = []
        for pt in scan.points:
            if MIN_RANGE <= pt.range <= MAX_RANGE:
                x, y = polar_to_xy(pt.angle, pt.range)
                points.append((x, y))

        # ── 배경 학습 단계 ──────────────────────────────
        if _bg_learning:
            learn_background(points)
            with _lock:
                _targets = []   # 학습 중에는 타겟 없음
            continue

        # ── 감지 단계: 배경 제거 후 클러스터링 ──────────
        moving_pts = filter_moving_points(points)
        centroids  = cluster_points(moving_pts)

        new_targets = []
        idx = 0
        for (cx, cy) in centroids:
            dist = math.hypot(cx, cy)
            if dist < MIN_TARGET_DIST:
                continue
            new_targets.append({
                "id": idx,
                "x":  round(cx * UNITY_SCALE, 2),
                "z":  round(cy * UNITY_SCALE, 2),
            })
            print(f"  [움직임] 타겟{idx}: ({cx:.2f}m, {cy:.2f}m) 거리={dist:.2f}m")
            idx += 1

        with _lock:
            _targets = new_targets

    laser.turnOff()
    laser.disconnecting()


# ── 좌표 변환 ──────────────────────────────────────────────────────────
def polar_to_xy(angle_rad: float, dist_m: float):
    return dist_m * math.cos(angle_rad), dist_m * math.sin(angle_rad)


def _run_simulation():
    """YDLIDAR SDK 없을 때 가짜 타겟으로 동작 테스트"""
    global _targets, _bg_learning
    import random

    # 시뮬레이션에서는 배경 학습 건너뜀
    print("[시뮬] 배경 학습 건너뜀 → 즉시 감지 모드")
    _bg_learning = False

    sim = [{"id": 0, "x": 4.0, "z": 6.0},
           {"id": 1, "x": -3.0, "z": 5.0}]
    while True:
        for t in sim:
            t["x"] += random.uniform(-0.2, 0.2)
            t["z"] += random.uniform(-0.2, 0.2)
        with _lock:
            _targets = [dict(t) for t in sim]
        time.sleep(0.1)


# ── WebSocket 서버 ─────────────────────────────────────────────────────
async def ws_handler(websocket):
    _clients.add(websocket)
    ip = websocket.remote_address
    print(f"[WS] Unity 접속: {ip}")
    try:
        async for _ in websocket:
            pass
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        _clients.discard(websocket)
        print(f"[WS] 연결 해제: {ip}")


async def broadcast_loop():
    interval = 1.0 / SEND_HZ
    while True:
        await asyncio.sleep(interval)
        if not _clients:
            continue
        with _lock:
            payload = json.dumps({"targets": list(_targets)})
        dead = set()
        for ws in list(_clients):
            try:
                await ws.send(payload)
            except Exception:
                dead.add(ws)
        _clients.difference_update(dead)


async def main():
    print(f"[서버] ws://0.0.0.0:{PORT} 대기 중...")
    t = threading.Thread(target=lidar_thread, daemon=True)
    t.start()
    async with websockets.serve(ws_handler, "0.0.0.0", PORT):
        await broadcast_loop()


if __name__ == "__main__":
    asyncio.run(main())
