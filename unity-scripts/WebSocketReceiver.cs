/*
 * WebSocketReceiver.cs
 * RP2040 WebSocket 서버에 연결하여 IMU 데이터를 수신한다.
 *
 * 필요 패키지: NativeWebSocket
 *   → Unity Package Manager → Add package from git URL:
 *   → https://github.com/endel/NativeWebSocket.git#upm
 *
 * 사용법:
 *   - 씬의 빈 GameObject에 이 스크립트를 추가
 *   - Inspector에서 ARDUINO_IP를 RP2040의 IP 주소로 변경
 *   - RocketController, RadarDisplay를 Inspector에 연결
 */

using System;
using UnityEngine;
using NativeWebSocket;

// JSON 파싱용 데이터 구조체
[Serializable]
public class SensorData
{
    public float pitch; // 앞뒤 기울기 (도)
    public float roll;  // 좌우 기울기 (도)
    public bool  fire;  // 발사 트리거
}

public class WebSocketReceiver : MonoBehaviour
{
    // ────────────────────────────────────────────
    // 설정 (Inspector에서 변경 가능)
    // ────────────────────────────────────────────

    [Header("연결 설정")]
    [Tooltip("Arduino Nano RP2040 Connect의 IP 주소")]
    public string arduinoIP   = "192.168.0.100";  // ← RP2040 IP로 변경

    [Tooltip("WebSocket 포트 (펌웨어 WS_PORT와 일치)")]
    public int    arduinoPort = 8080;

    [Tooltip("연결 실패 시 재시도 간격 (초)")]
    public float  reconnectDelay = 3f;

    [Header("연결할 스크립트")]
    public RocketController rocketController;  // 로켓 제어 스크립트
    public RadarDisplay     radarDisplay;      // 레이더 UI 스크립트

    // ────────────────────────────────────────────
    // 내부 변수
    // ────────────────────────────────────────────
    private WebSocket   _ws;
    private SensorData  _latestData = new SensorData();
    private bool        _isConnecting = false;

    // 외부에서 읽기 전용으로 최신 데이터 접근
    public SensorData LatestData => _latestData;

    // ────────────────────────────────────────────
    // Unity 라이프사이클
    // ────────────────────────────────────────────
    async void Start()
    {
        await ConnectAsync();
    }

    void Update()
    {
        // NativeWebSocket은 메인 스레드에서 메시지를 디스패치해야 함
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif
    }

    async void OnDestroy()
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
            await _ws.Close();
    }

    // ────────────────────────────────────────────
    // WebSocket 연결
    // ────────────────────────────────────────────
    private async System.Threading.Tasks.Task ConnectAsync()
    {
        if (_isConnecting) return;
        _isConnecting = true;

        string url = $"ws://{arduinoIP}:{arduinoPort}";
        Debug.Log($"[WS] 연결 시도: {url}");

        _ws = new WebSocket(url);

        // 연결 성공
        _ws.OnOpen += () =>
        {
            Debug.Log("[WS] RP2040 연결 성공!");
            _isConnecting = false;
        };

        // 메시지 수신
        _ws.OnMessage += (bytes) =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            ParseSensorData(json);
        };

        // 오류 발생
        _ws.OnError += (error) =>
        {
            Debug.LogWarning($"[WS] 오류: {error}");
        };

        // 연결 해제 → 재연결
        _ws.OnClose += (code) =>
        {
            Debug.LogWarning($"[WS] 연결 해제 (code: {code}). {reconnectDelay}초 후 재연결...");
            _isConnecting = false;
            Invoke(nameof(RetryConnect), reconnectDelay);
        };

        await _ws.Connect();
    }

    private async void RetryConnect()
    {
        await ConnectAsync();
    }

    // ────────────────────────────────────────────
    // JSON 파싱 및 데이터 전달
    // ────────────────────────────────────────────
    private void ParseSensorData(string json)
    {
        try
        {
            _latestData = JsonUtility.FromJson<SensorData>(json);

            // RocketController에 데이터 전달
            if (rocketController != null)
                rocketController.OnSensorData(_latestData);

            // RadarDisplay에 데이터 전달
            if (radarDisplay != null)
                radarDisplay.OnSensorData(_latestData);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WS] JSON 파싱 실패: {e.Message} | 원본: {json}");
        }
    }
}
