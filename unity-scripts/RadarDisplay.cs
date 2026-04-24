/*
 * RadarDisplay.cs
 * 화면 왼쪽 하단에 미니맵 스타일 레이더 UI를 그린다.
 * LiDAR 타겟 위치와 THAAD 발사대 방향을 레이더에 표시한다.
 *
 * 사용법:
 *   - 씬의 빈 GameObject에 이 스크립트를 추가
 *   - Inspector에서 launcher 연결 (없으면 자동 탐색)
 */

using UnityEngine;

public class RadarDisplay : MonoBehaviour
{
    // ────────────────────────────────────────────
    // Inspector 설정
    // ────────────────────────────────────────────

    [Header("레이더 위치 & 크기")]
    public int   marginX    = 20;
    public int   marginY    = 20;
    public int   radarRadius = 80;

    [Tooltip("레이더가 표현하는 실제 범위 (Unity 단위)")]
    public float radarRange = 50f;

    [Header("레이더 색상")]
    public Color backgroundColor = new Color(0f,  0.08f, 0f,  0.85f);
    public Color gridColor        = new Color(0f,  0.6f,  0f,  0.4f);
    public Color sweepColor       = new Color(0f,  1f,    0f,  0.8f);
    public Color targetColor      = new Color(1f,  0.1f,  0.1f, 1f);   // 타겟 (빨간색)
    public Color aimColor         = new Color(1f,  0.8f,  0f,  1f);    // 발사대 조준선 (노란색)
    public Color fireFlashColor   = new Color(1f,  0.2f,  0f,  1f);
    public Color textColor        = new Color(0f,  1f,    0f,  1f);

    [Header("스윕 속도")]
    public float sweepSpeed = 90f;

    [Header("연결")]
    [Tooltip("THAADLauncher (없으면 자동 탐색)")]
    public THAADLauncher launcher;

    // ────────────────────────────────────────────
    // 내부 변수
    // ────────────────────────────────────────────
    private float      _sweepAngle = 0f;
    private float      _flashTimer = 0f;
    private const float FLASH_DURATION = 0.5f;

    private Texture2D  _solidTex;

    // ────────────────────────────────────────────
    // Unity 라이프사이클
    // ────────────────────────────────────────────
    void Awake()
    {
        _solidTex = new Texture2D(1, 1);
        _solidTex.SetPixel(0, 0, Color.white);
        _solidTex.Apply();

        if (launcher == null)
            launcher = FindFirstObjectByType<THAADLauncher>();
    }

    void Update()
    {
        _sweepAngle = (_sweepAngle + sweepSpeed * Time.deltaTime) % 360f;

        if (_flashTimer > 0f)
            _flashTimer -= Time.deltaTime;
    }

    // 외부(THAADLauncher)에서 발사 이벤트를 받아 플래시 이펙트 트리거
    public void OnFired()
    {
        _flashTimer = FLASH_DURATION;
    }

    void OnDestroy()
    {
        if (_solidTex != null)
            Destroy(_solidTex);
    }

    // ────────────────────────────────────────────
    // OnGUI: 레이더 전체 그리기
    // ────────────────────────────────────────────
    void OnGUI()
    {
        int cx = marginX + radarRadius;
        int cy = Screen.height - marginY - radarRadius;

        DrawRadarBackground(cx, cy);
        DrawGridLines(cx, cy);
        DrawSweepLine(cx, cy);
        DrawTargets(cx, cy);
        DrawAimLine(cx, cy);
        DrawStatusText(cx, cy);

        if (_flashTimer > 0f)
            DrawFireFlash(cx, cy);
    }

    // ────────────────────────────────────────────
    // 레이더 배경
    // ────────────────────────────────────────────
    private void DrawRadarBackground(int cx, int cy)
    {
        DrawCircle(cx, cy, radarRadius + 2, new Color(0f, 0.8f, 0f, 0.9f), 2);

        GUI.color = backgroundColor;
        GUI.DrawTexture(
            new Rect(cx - radarRadius, cy - radarRadius, radarRadius * 2, radarRadius * 2),
            _solidTex);
        GUI.color = Color.white;

        DrawCircle(cx, cy, radarRadius / 3,     gridColor, 1);
        DrawCircle(cx, cy, radarRadius * 2 / 3, gridColor, 1);
        DrawCircle(cx, cy, radarRadius,          gridColor, 1);
    }

    private void DrawGridLines(int cx, int cy)
    {
        GUI.color = gridColor;
        GUI.DrawTexture(new Rect(cx - radarRadius, cy - 1, radarRadius * 2, 1), _solidTex);
        GUI.DrawTexture(new Rect(cx - 1, cy - radarRadius, 1, radarRadius * 2), _solidTex);
        GUI.color = Color.white;
    }

    private void DrawSweepLine(int cx, int cy)
    {
        float rad = _sweepAngle * Mathf.Deg2Rad;
        int ex = cx + Mathf.RoundToInt(Mathf.Cos(rad) * radarRadius);
        int ey = cy - Mathf.RoundToInt(Mathf.Sin(rad) * radarRadius);
        DrawLine(cx, cy, ex, ey, sweepColor, 2);
    }

    // ────────────────────────────────────────────
    // LiDAR 타겟을 레이더 위에 점으로 표시
    // ────────────────────────────────────────────
    private void DrawTargets(int cx, int cy)
    {
        if (launcher == null) return;

        Vector3 origin = launcher.transform.position;
        GameObject[] targets = GameObject.FindGameObjectsWithTag("Target");

        foreach (var t in targets)
        {
            Vector3 delta = t.transform.position - origin;
            // X-Z 평면 기준으로 레이더에 투영
            float nx = Mathf.Clamp(delta.x / radarRange, -1f, 1f);
            float nz = Mathf.Clamp(delta.z / radarRange, -1f, 1f);

            int dotX = cx + Mathf.RoundToInt(nx * radarRadius * 0.9f);
            int dotY = cy - Mathf.RoundToInt(nz * radarRadius * 0.9f);

            DrawCircle(dotX, dotY, 5, targetColor, 2);
            GUI.color = targetColor;
            GUI.DrawTexture(new Rect(dotX - 2, dotY - 2, 4, 4), _solidTex);
            GUI.color = Color.white;
        }
    }

    // ────────────────────────────────────────────
    // 발사대 포드 조준 방향 표시
    // ────────────────────────────────────────────
    private void DrawAimLine(int cx, int cy)
    {
        if (launcher == null) return;

        // PodYaw의 forward 방향을 레이더 위에 선으로 표시
        Transform podYaw = launcher.transform.Find("PodYaw");
        if (podYaw == null) return;

        // podYaw.forward는 타겟 반대 방향이므로 부호 반전
        Vector3 fwd = -podYaw.forward;
        float nx = fwd.x;
        float nz = fwd.z;
        float len = Mathf.Sqrt(nx * nx + nz * nz);
        if (len > 0.01f) { nx /= len; nz /= len; }

        int ex = cx + Mathf.RoundToInt(nx * radarRadius * 0.85f);
        int ey = cy - Mathf.RoundToInt(nz * radarRadius * 0.85f);

        DrawLine(cx, cy, ex, ey, aimColor, 2);
        DrawCircle(ex, ey, 4, aimColor, 2);
    }

    // ────────────────────────────────────────────
    // 발사 이펙트
    // ────────────────────────────────────────────
    private void DrawFireFlash(int cx, int cy)
    {
        float alpha = _flashTimer / FLASH_DURATION;
        Color flashCol = fireFlashColor;
        flashCol.a = alpha;

        DrawCircle(cx, cy, radarRadius,     flashCol, 3);
        DrawCircle(cx, cy, radarRadius + 4, flashCol, 2);

        GUIStyle fireStyle = new GUIStyle(GUI.skin.label);
        fireStyle.fontSize  = 28;
        fireStyle.fontStyle = FontStyle.Bold;
        fireStyle.normal.textColor = new Color(1f, 0.2f, 0f, alpha);
        fireStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Label(
            new Rect(Screen.width / 2f - 80, Screen.height / 2f - 20, 160, 40),
            "FIRE!", fireStyle);
    }

    // ────────────────────────────────────────────
    // 상태 텍스트
    // ────────────────────────────────────────────
    private void DrawStatusText(int cx, int cy)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 11;
        style.normal.textColor = textColor;
        style.alignment = TextAnchor.MiddleCenter;

        int targetCount = GameObject.FindGameObjectsWithTag("Target").Length;
        int textY = cy + radarRadius + 6;
        int textW = radarRadius * 2;

        GUI.Label(new Rect(cx - radarRadius, textY,      textW, 16),
                  $"탐지 타겟: {targetCount}", style);
        GUI.Label(new Rect(cx - radarRadius, textY + 16, textW, 16),
                  targetCount > 0 ? "[ 교전 중 ]" : "[ 대기 ]", style);
    }

    // ────────────────────────────────────────────
    // 유틸리티
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
