/*
 * RadarDisplay.cs
 * 화면 왼쪽 하단에 미니맵 스타일 레이더 UI를 그린다.
 * 현재 조준 방향을 레이더에 표시하고, 발사 시 이펙트를 출력한다.
 *
 * 사용법:
 *   - 씬의 빈 GameObject에 이 스크립트를 추가
 *   - 별도 프리팹/Canvas 불필요 — OnGUI로 직접 렌더링
 */

using UnityEngine;
using System.Collections;

public class RadarDisplay : MonoBehaviour
{
    // ────────────────────────────────────────────
    // Inspector 설정
    // ────────────────────────────────────────────

    [Header("레이더 위치 & 크기")]
    [Tooltip("화면 왼쪽 하단 기준 여백 (px)")]
    public int   marginX   = 20;
    public int   marginY   = 20;

    [Tooltip("레이더 원의 반지름 (px)")]
    public int   radarRadius = 80;

    [Header("레이더 색상")]
    public Color backgroundColor = new Color(0f,    0.08f, 0f,   0.85f); // 어두운 녹색 배경
    public Color gridColor        = new Color(0f,    0.6f,  0f,   0.4f);  // 그리드 선
    public Color sweepColor       = new Color(0f,    1f,    0f,   0.8f);  // 스윕 라인
    public Color aimDotColor      = new Color(1f,    0.8f,  0f,   1f);    // 조준점 (노란색)
    public Color fireFlashColor   = new Color(1f,    0.2f,  0f,   1f);    // 발사 이펙트 (빨간색)
    public Color textColor        = new Color(0f,    1f,    0f,   1f);    // 텍스트

    [Header("스윕 속도")]
    [Tooltip("레이더 스윕 회전 속도 (도/초)")]
    public float sweepSpeed = 90f;

    // ────────────────────────────────────────────
    // 내부 변수
    // ────────────────────────────────────────────
    private float   _pitch    = 0f;
    private float   _roll     = 0f;
    private bool    _fire     = false;

    private float   _sweepAngle   = 0f;      // 스윕 라인 현재 각도
    private float   _flashTimer   = 0f;      // 발사 이펙트 타이머
    private const float FLASH_DURATION = 0.5f;

    private Texture2D _solidTex;             // OnGUI 단색 드로잉용 텍스처

    // ────────────────────────────────────────────
    // Unity 라이프사이클
    // ────────────────────────────────────────────
    void Awake()
    {
        // 단색 텍스처 생성 (OnGUI DrawTexture에 사용)
        _solidTex = new Texture2D(1, 1);
        _solidTex.SetPixel(0, 0, Color.white);
        _solidTex.Apply();
    }

    void Update()
    {
        // 스윕 라인 회전
        _sweepAngle = (_sweepAngle + sweepSpeed * Time.deltaTime) % 360f;

        // 발사 이펙트 타이머 감소
        if (_flashTimer > 0f)
            _flashTimer -= Time.deltaTime;
    }

    void OnDestroy()
    {
        if (_solidTex != null)
            Destroy(_solidTex);
    }

    // ────────────────────────────────────────────
    // 외부에서 센서 데이터 수신 (WebSocketReceiver가 호출)
    // ────────────────────────────────────────────
    public void OnSensorData(SensorData data)
    {
        _pitch = data.pitch;
        _roll  = data.roll;

        // fire: false → true 전환 시 플래시 이펙트 시작
        if (data.fire && !_fire)
            _flashTimer = FLASH_DURATION;

        _fire = data.fire;
    }

    // ────────────────────────────────────────────
    // OnGUI: 레이더 전체 그리기
    // ────────────────────────────────────────────
    void OnGUI()
    {
        // 레이더 중심 좌표 (왼쪽 하단 기준)
        int cx = marginX + radarRadius;
        int cy = Screen.height - marginY - radarRadius;

        DrawRadarBackground(cx, cy);
        DrawGridLines(cx, cy);
        DrawSweepLine(cx, cy);
        DrawAimDot(cx, cy);
        DrawStatusText(cx, cy);

        // 발사 이펙트
        if (_flashTimer > 0f)
            DrawFireFlash(cx, cy);
    }

    // ────────────────────────────────────────────
    // 레이더 배경 (원형)
    // ────────────────────────────────────────────
    private void DrawRadarBackground(int cx, int cy)
    {
        // 외곽 테두리
        DrawCircle(cx, cy, radarRadius + 2, new Color(0f, 0.8f, 0f, 0.9f), 2);

        // 배경 채우기 (원 내부를 사각형으로 근사)
        GUI.color = backgroundColor;
        GUI.DrawTexture(
            new Rect(cx - radarRadius, cy - radarRadius,
                     radarRadius * 2,  radarRadius * 2),
            _solidTex
        );
        GUI.color = Color.white;

        // 동심원 그리드 (1/3, 2/3 크기)
        DrawCircle(cx, cy, radarRadius / 3,     gridColor, 1);
        DrawCircle(cx, cy, radarRadius * 2 / 3, gridColor, 1);
        DrawCircle(cx, cy, radarRadius,          gridColor, 1);
    }

    // ────────────────────────────────────────────
    // 십자선 그리드
    // ────────────────────────────────────────────
    private void DrawGridLines(int cx, int cy)
    {
        GUI.color = gridColor;
        // 수평선
        GUI.DrawTexture(new Rect(cx - radarRadius, cy - 1, radarRadius * 2, 1), _solidTex);
        // 수직선
        GUI.DrawTexture(new Rect(cx - 1, cy - radarRadius, 1, radarRadius * 2), _solidTex);
        GUI.color = Color.white;
    }

    // ────────────────────────────────────────────
    // 스윕 라인 (회전하는 레이더 스캔선)
    // ────────────────────────────────────────────
    private void DrawSweepLine(int cx, int cy)
    {
        float rad = _sweepAngle * Mathf.Deg2Rad;
        int ex = cx + Mathf.RoundToInt(Mathf.Cos(rad) * radarRadius);
        int ey = cy - Mathf.RoundToInt(Mathf.Sin(rad) * radarRadius);
        DrawLine(cx, cy, ex, ey, sweepColor, 2);
    }

    // ────────────────────────────────────────────
    // 조준점: pitch/roll을 레이더 좌표로 변환
    // pitch → Y축 (위아래), roll → X축 (좌우)
    // ────────────────────────────────────────────
    private void DrawAimDot(int cx, int cy)
    {
        const float maxAngle = 60f;  // 레이더 가장자리에 해당하는 최대 각도

        float nx = Mathf.Clamp(_roll  / maxAngle, -1f, 1f);  // -1 ~ 1 정규화
        float ny = Mathf.Clamp(_pitch / maxAngle, -1f, 1f);

        int dotX = cx + Mathf.RoundToInt(nx * radarRadius * 0.9f);
        int dotY = cy - Mathf.RoundToInt(ny * radarRadius * 0.9f);

        // 조준점 십자 마커
        int dotSize = 6;
        GUI.color = aimDotColor;
        GUI.DrawTexture(new Rect(dotX - dotSize, dotY - 1,  dotSize * 2, 2), _solidTex);
        GUI.DrawTexture(new Rect(dotX - 1,       dotY - dotSize, 2, dotSize * 2), _solidTex);
        GUI.color = Color.white;

        // 조준점 중심 원
        DrawCircle(dotX, dotY, 4, aimDotColor, 2);
    }

    // ────────────────────────────────────────────
    // 발사 이펙트: 레이더 테두리가 빨갛게 깜빡임
    // ────────────────────────────────────────────
    private void DrawFireFlash(int cx, int cy)
    {
        float alpha = _flashTimer / FLASH_DURATION;  // 1 → 0 페이드 아웃
        Color flashCol = fireFlashColor;
        flashCol.a = alpha;

        DrawCircle(cx, cy, radarRadius,     flashCol, 3);
        DrawCircle(cx, cy, radarRadius + 4, flashCol, 2);

        // 화면 중앙에 "FIRE!" 텍스트
        GUIStyle fireStyle = new GUIStyle(GUI.skin.label);
        fireStyle.fontSize  = 28;
        fireStyle.fontStyle = FontStyle.Bold;
        fireStyle.normal.textColor = new Color(1f, 0.2f, 0f, alpha);
        fireStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Label(
            new Rect(Screen.width / 2f - 80, Screen.height / 2f - 20, 160, 40),
            "🚀 FIRE!",
            fireStyle
        );
    }

    // ────────────────────────────────────────────
    // 상태 텍스트 (레이더 하단)
    // ────────────────────────────────────────────
    private void DrawStatusText(int cx, int cy)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 11;
        style.normal.textColor = textColor;
        style.alignment = TextAnchor.MiddleCenter;

        int textY = cy + radarRadius + 6;
        int textW = radarRadius * 2;

        GUI.Label(new Rect(cx - radarRadius, textY,      textW, 16),
                  $"P:{_pitch:+0.0;-0.0}° R:{_roll:+0.0;-0.0}°", style);

        GUI.Label(new Rect(cx - radarRadius, textY + 16, textW, 16),
                  _fire ? "[ 발사 준비 ]" : "[ 대기 ]", style);
    }

    // ────────────────────────────────────────────
    // 유틸리티: 원 그리기 (픽셀 근사)
    // ────────────────────────────────────────────
    private void DrawCircle(int cx, int cy, int radius, Color color, int thickness)
    {
        GUI.color = color;
        int steps = Mathf.Max(36, radius * 2);
        float step = 360f / steps;

        for (int i = 0; i < steps; i++)
        {
            float a1 = i * step * Mathf.Deg2Rad;
            float a2 = (i + 1) * step * Mathf.Deg2Rad;

            int x1 = cx + Mathf.RoundToInt(Mathf.Cos(a1) * radius);
            int y1 = cy - Mathf.RoundToInt(Mathf.Sin(a1) * radius);
            int x2 = cx + Mathf.RoundToInt(Mathf.Cos(a2) * radius);
            int y2 = cy - Mathf.RoundToInt(Mathf.Sin(a2) * radius);

            DrawLine(x1, y1, x2, y2, color, thickness);
        }

        GUI.color = Color.white;
    }

    // ────────────────────────────────────────────
    // 유틸리티: 선 그리기 (브레젠험 근사)
    // ────────────────────────────────────────────
    private void DrawLine(int x1, int y1, int x2, int y2, Color color, int width)
    {
        GUI.color = color;

        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = Mathf.Sqrt(dx * dx + dy * dy);

        if (length < 0.001f) return;

        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

        GUIUtility.RotateAroundPivot(angle, new Vector2(x1, y1));
        GUI.DrawTexture(new Rect(x1, y1 - width / 2f, length, width), _solidTex);
        GUIUtility.RotateAroundPivot(-angle, new Vector2(x1, y1));

        GUI.color = Color.white;
    }
}
