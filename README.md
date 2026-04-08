# 🛡️ 3D Air Defense Radar System
### Arduino Nano RP2040 Connect + Unity 3D 실시간 방공 시뮬레이션

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Arduino%20Nano%20RP2040-00979D?style=for-the-badge&logo=arduino&logoColor=white"/>
  <img src="https://img.shields.io/badge/Engine-Unity%203D-000000?style=for-the-badge&logo=unity&logoColor=white"/>
  <img src="https://img.shields.io/badge/Language-C%2B%2B%20%7C%20C%23-blueviolet?style=for-the-badge"/>
  <img src="https://img.shields.io/badge/Communication-WiFi%20WebSocket-orange?style=for-the-badge"/>
  <img src="https://img.shields.io/badge/Type-Graduation%20Project-red?style=for-the-badge"/>
</p>

---

## 📌 개요

실제 물체를 초음파 센서로 감지하고, IMU 센서로 조준 방향을 입력받아
Unity 3D 화면에서 실시간 방공 레이더 시뮬레이션을 구현한 **임베디드-가상현실 연동 시스템**입니다.

> "현실 세계의 센서 데이터가 가상 3D 공간에서 즉각적으로 반응한다"

---

## 🎯 개발 목적

| 목적 | 설명 |
|------|------|
| **임베디드-가상현실 연동** | 실물 하드웨어와 Unity 3D 엔진의 실시간 양방향 통신 구현 |
| **실시간 센서 시각화** | 초음파/IMU 센서 데이터를 3D 공간에 즉각 반영 |
| **자율 감지 및 반응 시스템** | 위협 감지 → 판단 → 요격이라는 목적 있는 자동화 흐름 구현 |
| **포트폴리오** | 임베디드 펌웨어 + 무선 통신 + 3D 렌더링을 단일 시스템으로 통합 |

---

## 🏗️ 시스템 아키텍처

```mermaid
flowchart TB
    subgraph HW["🔧 Hardware Layer (Arduino Nano RP2040 Connect)"]
        US["초음파 센서\nHC-SR04\n물체 감지 / 거리 측정"]
        IMU["IMU 센서\nLSM6DSOX 내장\n6축 기울기 / 가속도"]
        WIFI["WiFi 모듈\nu-blox NINA-W102 내장\n무선 데이터 송신"]
        MCU["RP2040\n메인 컨트롤러"]
    end

    subgraph NET["📡 Network Layer"]
        WS["WebSocket Server\nPython / Node.js"]
    end

    subgraph SW["🎮 Software Layer (Unity 3D)"]
        WSC["WebSocket Client\nC# Script"]
        RADAR["3D 레이더 화면\n위협 오브젝트 렌더링"]
        AIM["조준 시스템\nIMU 기울기 → 조준선"]
        INT["요격 시스템\n미사일 발사 애니메이션"]
    end

    US --> MCU
    IMU --> MCU
    MCU --> WIFI
    WIFI -- "WebSocket\nJSON 데이터" --> WS
    WS --> WSC
    WSC --> RADAR
    WSC --> AIM
    AIM --> INT
```

---

## 🔄 데이터 흐름도

```mermaid
sequenceDiagram
    participant US as 초음파 센서
    participant RP as RP2040
    participant IMU as IMU 센서
    participant WS as WebSocket
    participant Unity as Unity 3D

    loop 실시간 스캔 (50ms 주기)
        RP->>US: 서보모터 각도 제어 (0°~180°)
        US->>RP: 거리값 반환 (cm)
        RP->>IMU: 기울기 데이터 요청
        IMU->>RP: X/Y/Z 축 각도 반환
        RP->>RP: 위협 판단(거리 < 임계값?)

        alt 위협 감지됨
            RP->>WS: JSON 전송 {angle, distance, imu_x, imu_y, threat: true}
            WS->>Unity: 데이터 수신
            Unity->>Unity: 레이더에 위협 오브젝트 표시
            Unity->>Unity: 경보 이펙트 활성화
            Note over Unity: IMU 기울기로 조준선 이동
            Note over Unity: 임계 입력 시 요격 미사일 발사
        else 위협 없음
            RP->>WS: JSON 전송 {angle, distance, threat: false}
            WS->>Unity: 데이터 수신
            Unity->>Unity: 레이더 스캔 라인 업데이트
        end
    end
```

---

## 🧩 시스템 구성 요소

```mermaid
flowchart LR
    subgraph IN["입력"]
        US2["초음파 센서\nHC-SR04"]
        IMU2["IMU 내장\nLSM6DSOX"]
    end

    subgraph PROC["처리"]
        MCU2["RP2040\n펌웨어"]
        DIST["거리/각도\n계산"]
        THREAT["위협 감지\n알고리즘"]
        WIFI2["WiFi\nWebSocket\nJSON직렬화"]
    end

    subgraph OUT["출력 - Unity 3D"]
        DISP["3D 레이더\n디스플레이"]
        OBJ["위협 오브젝트\n렌더링"]
        AIM2["조준 시스템"]
        INT2["요격 애니메이션"]
    end

    US2 --> MCU2
    IMU2 --> MCU2
    MCU2 --> DIST
    MCU2 --> THREAT
    DIST --> WIFI2
    THREAT --> WIFI2
    WIFI2 --> DISP
    WIFI2 --> OBJ
    WIFI2 --> AIM2
    AIM2 --> INT2
```

---

## 🛠️ 기술 스택

### Hardware

| 구성 요소 | 모델 | 역할 |
|-----------|------|------|
| **메인 보드** | Arduino Nano RP2040 Connect | 메인 컨트롤러 |
| **거리 센서** | HC-SR04 초음파 센서 | 물체 감지 및 거리 측정 |
| **IMU** | LSM6DSOX (내장) | 기울기 기반 조준 입력 |
| **통신** | u-blox NINA-W102 (내장) | WiFi WebSocket 통신 |

### Software

| 구성 요소 | 기술 | 역할 |
|-----------|------|------|
| **펌웨어** | Arduino C++ | 센서 제어 및 데이터 처리 |
| **통신 프로토콜** | WebSocket + JSON | 실시간 양방향 통신 |
| **3D 엔진** | Unity 2022 LTS (C#) | 3D 레이더 화면 및 시뮬레이션 |
| **데이터 포맷** | JSON | 센서 데이터 직렬화 |

---

## 📁 프로젝트 구조

```
3D-Air-Defense-Radar/
│
├── 📂 firmware/                    # Arduino RP2040 펌웨어
│   ├── main.ino                    # 메인 루프
│   ├── sensor/
│   │   ├── ultrasonic.h           # 초음파 센서 드라이버
│   │   └── imu.h                  # IMU 센서 드라이버
│   ├── comm/
│   │   └── websocket_client.h     # WiFi WebSocket 송신
│   └── config.h                   # 핀 설정, 임계값 상수
│
├── 📂 unity-project/               # Unity 3D 프로젝트
│   └── Assets/
│       ├── Scripts/
│       │   ├── WebSocketReceiver.cs    # 데이터 수신
│       │   ├── RadarDisplay.cs         # 3D 레이더 렌더링
│       │   ├── ThreatManager.cs        # 위협 오브젝트 관리
│       │   ├── AimController.cs        # IMU 기반 조준 제어
│       │   └── InterceptSystem.cs      # 요격 미사일 시스템
│       ├── Prefabs/
│       │   ├── ThreatObject.prefab     # 위협 오브젝트
│       │   └── Missile.prefab          # 요격 미사일
│       └── Scenes/
│           └── RadarScene.unity        # 메인 씬
│
└── 📂 docs/                        # 문서
    ├── wiring_diagram.png          # 배선도
    └── demo.gif                    # 데모 영상
```

---

## 🔌 배선도

```
Arduino Nano RP2040 Connect
│
├── D2  ──── HC-SR04 TRIG
├── D3  ──── HC-SR04 ECHO
├── 5V  ──── HC-SR04 VCC
├── GND ──── HC-SR04 GND
│
├── [IMU LSM6DSOX - 내장, 배선 불필요]
└── [WiFi u-blox  - 내장, 배선 불필요]
```

> ⚡ 초음파 센서 4핀 연결이 외부 배선의 전부입니다.

---

## 🚀 개발 로드맵

```mermaid
gantt
    title 개발 일정 (12주)
    dateFormat  YYYY-MM-DD
    section 1단계 - 환경 구성
    개발 환경 세팅          :done, w1, 2025-04-14, 1w
    하드웨어 연결 및 테스트  :done, w2, 2025-04-21, 1w
    section 2단계 - 펌웨어
    초음파 센서 드라이버     :active, w3, 2025-04-28, 1w
    IMU 데이터 수집          :w4, 2025-05-05, 1w
    WiFi WebSocket 송신      :w5, 2025-05-12, 1w
    section 3단계 - Unity
    WebSocket 수신 구현      :w6, 2025-05-19, 1w
    3D 레이더 디스플레이     :w7, 2025-05-26, 2w
    요격 시스템 구현         :w9, 2025-06-09, 1w
    section 4단계 - 통합
    전체 시스템 통합 테스트  :w10, 2025-06-16, 1w
    데모 및 발표 준비        :w11, 2025-06-23, 1w
```

---

## 🎮 데모 시나리오

```
1. 시스템 부팅
   └─ RP2040 WiFi 연결 → Unity 3D 레이더 화면 활성화

2. 스캔 시작
   └─ 초음파 센서 자동 180° 스캔
   └─ Unity 레이더에 스캔 라인 실시간 회전

3. 위협 감지
   └─ 손/물체를 센서 앞에 가져다 댐
   └─ Unity 레이더에 붉은 위협 오브젝트 즉각 표시
   └─ 경보음 + 이펙트 활성화

4. 요격
   └─ 보드 기울여서 조준선 이동 (IMU)
   └─ 조준 완료 시 요격 미사일 발사 애니메이션
   └─ 위협 오브젝트 소멸 이펙트
```

---

## 📊 기대 성능 목표

| 항목 | 목표값 |
|------|--------|
| **센서 스캔 주기** | 50ms 이하 |
| **WiFi 전송 지연** | 100ms 이하 |
| **Unity 렌더링** | 60 FPS 유지 |
| **감지 거리** | 5cm ~ 200cm |
| **IMU 반응 지연** | 30ms 이하 |

---

## 👨‍💻 개발자

| 항목 | 내용 |
|------|------|
| **이름** | 창연 |
| **학과** | 임베디드소프트웨어학과 |
| **GitHub** | [@changyeon47](https://github.com/changyeon47) |
| **유형** | 개인 졸업작품 |

---

## 📄 라이선스

MIT License © 2025 changyeon47
