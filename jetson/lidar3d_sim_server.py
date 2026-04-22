#!/usr/bin/env python3
"""
lidar3d_sim_server.py
3D LiDAR 시뮬레이터 + WebSocket 서버

실제 LiDAR 없이 VLP-16 스타일 3D 스캔을 시뮬레이션하여 Unity로 전송합니다.
기존 lidar_server.py(2D YDLIDAR)를 3D 시뮬레이터로 대체합니다.

좌표계: x=동(East), y=위(Up), z=북(North)  ← Unity와 동일
센서 위치: 원점 (0, 0, 0) — 지상 설치

타겟: 공중을 이동하는 드론/미사일 (여러 개, 자동 순환)
배경: 지상 정적 구조물 → 배경 학습 후 필터링

출력 JSON: {"targets": [{"id": int, "x": float, "y": float, "z": float}, ...]}

사용:
  pip install websockets numpy
  python lidar3d_sim_server.py
"""

import asyncio
import json
import math
import random
import threading
import time
import websockets
import numpy as np

# ── 서버 설정 ──────────────────────────────────────────────────────────────
PORT      = 8081
SEND_HZ   = 10

# ── 3D LiDAR 스캔 파라미터 (VLP-16 스타일, 대공 특화) ─────────────────────
N_CHANNELS     = 16       # 수직 채널 수
ELEV_MIN_DEG   =  5.0     # 최소 앙각 (°) — 지면 클러터 제거
ELEV_MAX_DEG   = 75.0     # 최대 앙각 (°) — 주로 상공 탐색
AZIM_STEP_DEG  =  1.0     # 수평 해상도 (°) → 360개 방위각
MAX_RANGE      = 150.0    # 최대 탐지 거리 (m)
MIN_RANGE      =  1.0     # 최소 탐지 거리 (m)
RANGE_NOISE_SD =  0.05    # 거리 노이즈 표준편차 (m)

# ── 배경 제거 파라미터 ─────────────────────────────────────────────────────
BG_LEARN_FRAMES = 20      # 배경 학습 프레임 수 (~2초)
BG_VOXEL_SIZE   = 1.0     # 배경 복셀 크기 (m) — 이 크기 격자로 정적 점 등록

# ── 3D 클러스터링 파라미터 ─────────────────────────────────────────────────
CLUSTER_DIST    = 4.0     # 클러스터 묶음 거리 (m)
MIN_CLUSTER_PTS = 3       # 클러스터 최소 포인트 수

# ── 타겟 시뮬레이션 파라미터 ───────────────────────────────────────────────
TARGET_RADIUS   = 1.5     # 타겟 반지름 (m) — 드론/소형 무인기
N_TARGETS       = 3       # 동시 공중 타겟 수
ARENA_SIZE      = 80.0    # 타겟 이동 영역 반지름 (m)
ALTITUDE_MIN    = 20.0    # 최소 고도 (m)
ALTITUDE_MAX    = 60.0    # 최대 고도 (m)
TARGET_SPEED    = 8.0     # 평균 속도 (m/s)

# ── 정적 배경 구조물 (x, y_center, z, radius) ─────────────────────────────
# 배경 학습 시 등록 → 탐지 단계에서 필터링됨
_STATIC_OBJECTS = [
    ( 28,  8,  38, 5.0),   # 북동쪽 건물
    (-35,  6,  22, 4.0),   # 북서쪽 건물
    ( 10,  5, -32, 6.0),   # 남쪽 건물
    (-22,  4, -28, 3.0),   # 남서쪽 건물
]

# ── 공유 상태 ──────────────────────────────────────────────────────────────
_lock        = threading.Lock()
_targets_out = []    # Unity로 내보낼 타겟 목록
_clients     = set()

_bg_voxels   = set()
_bg_count    = 0
_bg_learning = True


# ─────────────────────────────────────────────────────────────────────────────
# 스캔 레이 사전 계산 (numpy)
# ─────────────────────────────────────────────────────────────────────────────

def _build_rays_np() -> np.ndarray:
    """(N_RAYS, 3) 단위 방향 벡터 배열 반환"""
    elevs = np.linspace(ELEV_MIN_DEG, ELEV_MAX_DEG, N_CHANNELS)
    azims = np.arange(0, 360, AZIM_STEP_DEG)
    er = np.radians(elevs)
    ar = np.radians(azims)

    # 브로드캐스트: elevation(16,) × azimuth(360,) → (16, 360, 3)
    ce = np.cos(er)[:, None]  # (16, 1)
    se = np.sin(er)[:, None]
    sa = np.sin(ar)[None, :]  # (1, 360)
    ca = np.cos(ar)[None, :]

    dx = ce * sa    # (16, 360)
    dy = np.broadcast_to(se, (N_CHANNELS, len(azims)))
    dz = ce * ca

    rays = np.stack([dx, dy, dz], axis=-1).reshape(-1, 3)  # (5760, 3)
    return rays.astype(np.float32)

_RAYS = _build_rays_np()
N_RAYS = len(_RAYS)
print(f"[LiDAR3D] 스캔 레이 {N_RAYS}개 ({N_CHANNELS}채널 × {int(360/AZIM_STEP_DEG)}방위각)")


# ─────────────────────────────────────────────────────────────────────────────
# 타겟 클래스
# ─────────────────────────────────────────────────────────────────────────────

class _SphereObject:
    """구 형태의 반사체 (정적/이동 공통)"""
    def __init__(self, x, y, z, radius):
        self.x, self.y, self.z = float(x), float(y), float(z)
        self.radius = float(radius)

    @property
    def pos(self) -> np.ndarray:
        return np.array([self.x, self.y, self.z], dtype=np.float32)


class AerialTarget(_SphereObject):
    def __init__(self, tid: int):
        super().__init__(0, 0, 0, TARGET_RADIUS)
        self.id = tid
        self._respawn()

    def _respawn(self):
        angle = random.uniform(0, 2 * math.pi)
        dist  = random.uniform(ARENA_SIZE * 0.6, ARENA_SIZE)
        self.x = dist * math.sin(angle)
        self.y = random.uniform(ALTITUDE_MIN, ALTITUDE_MAX)
        self.z = dist * math.cos(angle)

        # 센서 방향으로 접근하는 속도 벡터
        to_sensor = math.atan2(-self.x, -self.z)
        speed = random.uniform(TARGET_SPEED * 0.7, TARGET_SPEED * 1.4)
        self.vx = speed * math.sin(to_sensor) + random.uniform(-2.0, 2.0)
        self.vy = random.uniform(-1.5, 1.5)
        self.vz = speed * math.cos(to_sensor) + random.uniform(-2.0, 2.0)

    def update(self, dt: float):
        self.x += self.vx * dt
        self.y += self.vy * dt
        self.z += self.vz * dt

        # 고도 클램프
        if self.y < ALTITUDE_MIN:
            self.y = ALTITUDE_MIN
            self.vy = abs(self.vy)
        elif self.y > ALTITUDE_MAX:
            self.y = ALTITUDE_MAX
            self.vy = -abs(self.vy)

        # 센서에 너무 가깝거나 벗어나면 재생성
        horiz = math.sqrt(self.x ** 2 + self.z ** 2)
        if horiz < 6.0 or horiz > ARENA_SIZE * 1.6:
            self._respawn()


# ─────────────────────────────────────────────────────────────────────────────
# 3D 스캔 (numpy 벡터화 레이-구 교차)
# ─────────────────────────────────────────────────────────────────────────────

def _ray_sphere_hits(obj: _SphereObject, depth: np.ndarray) -> np.ndarray:
    """
    모든 레이와 구의 교차를 계산하여 depth 버퍼를 갱신.
    depth: (N_RAYS,) 현재 가장 가까운 히트 거리 버퍼 (in-place 수정)
    """
    C  = obj.pos                          # (3,)
    dc = _RAYS @ C                        # (N_RAYS,)  D·C
    c2 = float(np.dot(C, C))
    disc = dc * dc - (c2 - obj.radius ** 2)

    hit   = disc >= 0
    t_val = dc - np.sqrt(np.where(hit, disc, 0.0))
    valid = hit & (t_val >= MIN_RANGE) & (t_val <= MAX_RANGE) & (t_val < depth)
    depth[valid] = t_val[valid]
    return depth


def do_scan(mobile: list, static: list, learning: bool) -> list:
    """
    3D LiDAR 스캔 1회 시뮬레이션.
    Returns: [(x, y, z), ...] 히트 포인트 목록
    """
    objects = list(static)
    if not learning:
        objects.extend(mobile)

    if not objects:
        return []

    depth = np.full(N_RAYS, MAX_RANGE + 1.0, dtype=np.float32)
    for obj in objects:
        _ray_sphere_hits(obj, depth)

    hit_mask = depth <= MAX_RANGE
    if not np.any(hit_mask):
        return []

    noise = np.random.normal(0, RANGE_NOISE_SD, int(hit_mask.sum())).astype(np.float32)
    pts = _RAYS[hit_mask] * (depth[hit_mask] + noise)[:, None]  # (K, 3)
    return pts.tolist()


# ─────────────────────────────────────────────────────────────────────────────
# 3D 배경 복셀 (격자 기반)
# ─────────────────────────────────────────────────────────────────────────────

def _to_voxel(x: float, y: float, z: float) -> tuple:
    s = BG_VOXEL_SIZE
    return (int(x / s), int(y / s), int(z / s))


def learn_background_3d(points: list):
    global _bg_count, _bg_learning
    for (x, y, z) in points:
        vx, vy, vz = _to_voxel(x, y, z)
        # 인접 복셀까지 등록 (센서 노이즈 흡수)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    _bg_voxels.add((vx + dx, vy + dy, vz + dz))

    _bg_count += 1
    rem = BG_LEARN_FRAMES - _bg_count
    if rem > 0:
        print(f"[배경 학습] {_bg_count}/{BG_LEARN_FRAMES} — 타겟 없음 ({rem}회 남음)")
    else:
        _bg_learning = False
        print(f"[배경 학습 완료] 복셀 {len(_bg_voxels)}개 등록 → 탐지 시작")


def filter_moving_3d(points: list) -> list:
    """배경에 없는 점(이동 물체)만 반환"""
    return [(x, y, z) for (x, y, z) in points
            if _to_voxel(x, y, z) not in _bg_voxels]


# ─────────────────────────────────────────────────────────────────────────────
# 3D 클러스터링 (거리 기반)
# ─────────────────────────────────────────────────────────────────────────────

def cluster_3d(points: list) -> list:
    """3D 거리 기반 클러스터링 → 클러스터 중심 반환"""
    if not points:
        return []

    pts   = np.array(points, dtype=np.float32)   # (N, 3)
    n     = len(pts)
    used  = np.zeros(n, dtype=bool)
    centroids = []

    # 쌍별 거리 행렬 (N이 작으면 충분히 빠름)
    diff  = pts[:, None, :] - pts[None, :, :]    # (N, N, 3)
    dists = np.sqrt((diff ** 2).sum(axis=2))     # (N, N)

    for i in range(n):
        if used[i]:
            continue
        neighbors = np.where(~used & (dists[i] < CLUSTER_DIST))[0]
        if len(neighbors) >= MIN_CLUSTER_PTS:
            used[neighbors] = True
            centroid = pts[neighbors].mean(axis=0)
            centroids.append(centroid.tolist())

    return centroids


# ─────────────────────────────────────────────────────────────────────────────
# 메인 시뮬레이션 스레드
# ─────────────────────────────────────────────────────────────────────────────

def simulation_thread():
    global _targets_out

    mobile  = [AerialTarget(i) for i in range(N_TARGETS)]
    static  = [_SphereObject(*obj) for obj in _STATIC_OBJECTS]
    interval = 1.0 / SEND_HZ

    print(f"[시뮬] 동적 타겟 {N_TARGETS}개, 정적 구조물 {len(static)}개")
    print(f"[시뮬] 배경 학습 시작 ({BG_LEARN_FRAMES}프레임, 타겟 없음)...")

    last_t = time.perf_counter()

    while True:
        now = time.perf_counter()
        dt  = now - last_t
        last_t = now

        for t in mobile:
            t.update(dt)

        # ── 스캔 ─────────────────────────────────────────────────────────
        hit_pts = do_scan(mobile, static, _bg_learning)

        if _bg_learning:
            learn_background_3d(hit_pts)
            with _lock:
                _targets_out = []
        else:
            # 배경 제거 → 클러스터링
            moving_pts = filter_moving_3d(hit_pts)
            centroids  = cluster_3d(moving_pts)

            new_out = []
            for idx, (cx, cy, cz) in enumerate(centroids):
                new_out.append({
                    "id": idx,
                    "x":  round(cx, 2),
                    "y":  round(cy, 2),
                    "z":  round(cz, 2),
                })
            with _lock:
                _targets_out = new_out

            if new_out:
                for t in new_out:
                    print(f"  [탐지] 타겟{t['id']}: "
                          f"({t['x']:.1f}, {t['y']:.1f}, {t['z']:.1f})m  "
                          f"히트={len(moving_pts)}pts 클러스터={len(centroids)}")

        elapsed = time.perf_counter() - now
        sleep_t = max(0.0, interval - elapsed)
        time.sleep(sleep_t)


# ─────────────────────────────────────────────────────────────────────────────
# WebSocket 서버
# ─────────────────────────────────────────────────────────────────────────────

async def ws_handler(websocket):
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


async def broadcast_loop():
    interval = 1.0 / SEND_HZ
    while True:
        await asyncio.sleep(interval)
        if not _clients:
            continue
        with _lock:
            payload = json.dumps({"targets": list(_targets_out)})
        dead = set()
        for ws in list(_clients):
            try:
                await ws.send(payload)
            except Exception:
                dead.add(ws)
        _clients.difference_update(dead)


async def main():
    print(f"[서버] ws://0.0.0.0:{PORT} 대기 중...")
    t = threading.Thread(target=simulation_thread, daemon=True)
    t.start()
    async with websockets.serve(ws_handler, "0.0.0.0", PORT):
        await broadcast_loop()


if __name__ == "__main__":
    print("=" * 60)
    print(" 3D LiDAR 시뮬레이터 서버")
    print(f" 채널: {N_CHANNELS}  해상도: {AZIM_STEP_DEG}°  레이: {N_RAYS}개")
    print(f" 타겟: {N_TARGETS}개  고도: {ALTITUDE_MIN}~{ALTITUDE_MAX}m")
    print(f" 포트: ws://localhost:{PORT}")
    print("=" * 60)
    asyncio.run(main())
