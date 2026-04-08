/*
 * 3D Air Defense Radar System - Arduino 펌웨어
 * 보드: Arduino Nano RP2040 Connect
 *
 * 기능:
 *   - LSM6DSOX 내장 IMU에서 pitch/roll 각도 읽기
 *   - WiFi WebSocket 서버 열기 (포트 8080)
 *   - 50ms 주기로 JSON 전송: {"pitch": X, "roll": Y, "fire": bool}
 *   - pitch >= 30도이면 fire: true
 *
 * 필요 라이브러리 (Arduino Library Manager에서 설치):
 *   - WiFiNINA
 *   - Arduino_LSM6DSOX
 *   - ArduinoWebSockets (by Links2004)
 */

#include <WiFiNINA.h>
#include <WebSocketsServer.h>
#include <Arduino_LSM6DSOX.h>

// ────────────────────────────────────────────
// 설정 상수 (환경에 맞게 수정)
// ────────────────────────────────────────────
const char* WIFI_SSID     = "YOUR_WIFI_SSID";      // WiFi 이름
const char* WIFI_PASSWORD = "YOUR_WIFI_PASSWORD";  // WiFi 비밀번호
const int   WS_PORT       = 8080;                  // WebSocket 서버 포트
const int   SEND_INTERVAL = 50;                    // 전송 주기 (ms)
const float FIRE_THRESHOLD = 30.0f;                // 발사 트리거 pitch 임계값 (도)

// ────────────────────────────────────────────
// 전역 변수
// ────────────────────────────────────────────
WebSocketsServer webSocket(WS_PORT);

float pitch = 0.0f;
float roll  = 0.0f;
bool  fire  = false;

unsigned long lastSendTime = 0;
bool clientConnected = false;

// ────────────────────────────────────────────
// WebSocket 이벤트 콜백
// ────────────────────────────────────────────
void onWebSocketEvent(uint8_t clientId, WStype_t type, uint8_t* payload, size_t length) {
  switch (type) {
    case WStype_CONNECTED:
      Serial.print("[WS] Unity 클라이언트 연결됨. ID: ");
      Serial.println(clientId);
      clientConnected = true;
      break;

    case WStype_DISCONNECTED:
      Serial.print("[WS] 클라이언트 연결 해제. ID: ");
      Serial.println(clientId);
      clientConnected = false;
      break;

    case WStype_TEXT:
      // Unity에서 메시지 수신 (필요 시 처리)
      Serial.print("[WS] 수신: ");
      Serial.println((char*)payload);
      break;

    default:
      break;
  }
}

// ────────────────────────────────────────────
// IMU에서 pitch/roll 계산
// 가속도 데이터 기반 정적 기울기 계산
// ────────────────────────────────────────────
void readIMU() {
  float ax, ay, az;

  if (IMU.accelerationAvailable()) {
    IMU.readAcceleration(ax, ay, az);

    // pitch: X축 기울기 (앞뒤)
    pitch = atan2(ax, sqrt(ay * ay + az * az)) * 180.0f / PI;

    // roll: Y축 기울기 (좌우)
    roll  = atan2(ay, sqrt(ax * ax + az * az)) * 180.0f / PI;
  }
}

// ────────────────────────────────────────────
// JSON 문자열 생성
// {"pitch": 15.3, "roll": -8.2, "fire": false}
// ────────────────────────────────────────────
String buildJson() {
  String json = "{";
  json += "\"pitch\":" + String(pitch, 2) + ",";
  json += "\"roll\":"  + String(roll, 2)  + ",";
  json += "\"fire\":"  + (fire ? "true" : "false");
  json += "}";
  return json;
}

// ────────────────────────────────────────────
// WiFi 연결
// ────────────────────────────────────────────
void connectWiFi() {
  Serial.print("[WiFi] 연결 중: ");
  Serial.println(WIFI_SSID);

  while (WiFi.begin(WIFI_SSID, WIFI_PASSWORD) != WL_CONNECTED) {
    Serial.print(".");
    delay(1000);
  }

  Serial.println("\n[WiFi] 연결 완료!");
  Serial.print("[WiFi] IP 주소: ");
  Serial.println(WiFi.localIP());
  Serial.print("[WS]  WebSocket 주소: ws://");
  Serial.print(WiFi.localIP());
  Serial.print(":");
  Serial.println(WS_PORT);
}

// ────────────────────────────────────────────
// setup
// ────────────────────────────────────────────
void setup() {
  Serial.begin(115200);
  while (!Serial && millis() < 3000);  // 시리얼 모니터 대기 (최대 3초)

  Serial.println("=== 3D Air Defense Radar Firmware ===");

  // IMU 초기화
  if (!IMU.begin()) {
    Serial.println("[오류] LSM6DSOX IMU 초기화 실패!");
    while (true);
  }
  Serial.println("[IMU] LSM6DSOX 초기화 완료");

  // WiFi 연결
  connectWiFi();

  // WebSocket 서버 시작
  webSocket.begin();
  webSocket.onEvent(onWebSocketEvent);
  Serial.println("[WS] WebSocket 서버 시작");
}

// ────────────────────────────────────────────
// loop
// ────────────────────────────────────────────
void loop() {
  webSocket.loop();

  unsigned long now = millis();
  if (now - lastSendTime >= SEND_INTERVAL) {
    lastSendTime = now;

    // IMU 데이터 읽기
    readIMU();

    // 발사 판단: pitch가 임계값 이상이면 fire = true
    fire = (pitch >= FIRE_THRESHOLD);

    // JSON 생성 및 브로드캐스트
    String json = buildJson();
    webSocket.broadcastTXT(json);

    // 시리얼 모니터 출력
    Serial.print("[DATA] ");
    Serial.println(json);
  }
}
