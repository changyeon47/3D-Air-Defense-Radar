/*
 * AimCamera.cs
 * 런처 포드의 조준 방향을 1인칭으로 보여주는 PIP 카메라.
 * 화면 우상단에 표시된다.
 *
 * 사용법:
 *   - 빈 GameObject에 이 스크립트 추가
 */

using UnityEngine;

public class AimCamera : MonoBehaviour
{
    [Header("PIP 위치 & 크기 (0~1 비율)")]
    public float pipX      = 0.68f;
    public float pipY      = 0.72f;   // 우상단
    public float pipWidth  = 0.30f;
    public float pipHeight = 0.26f;

    [Header("카메라 설정")]
    public float fov       = 40f;     // 좁을수록 줌인 효과
    public float smoothing = 8f;      // 회전 보간 속도

    private Camera    _cam;
    private Transform _podPitch;      // 런처 포드 피치 트랜스폼
    private Texture2D _whiteTex;

    void Awake()
    {
        // 카메라 오브젝트 생성
        GameObject camObj = new GameObject("AimCam");
        _cam = camObj.AddComponent<Camera>();
        _cam.rect            = new Rect(pipX, pipY, pipWidth, pipHeight);
        _cam.depth           = 2;                              // OverviewCamera(depth=1)보다 위
        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.05f, 0.08f, 0.05f);
        _cam.fieldOfView     = fov;
        _cam.farClipPlane    = 500f;
        _cam.nearClipPlane   = 0.3f;

        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();
    }

    void LateUpdate()
    {
        // PodPitch 캐싱 (Awake에서는 아직 THAADLauncher가 빌드 전일 수 있음)
        if (_podPitch == null)
        {
            THAADLauncher launcher = FindFirstObjectByType<THAADLauncher>();
            if (launcher != null)
                _podPitch = launcher.GetPodPitch();
            if (_podPitch == null) return;
        }

        // podYaw가 -toTarget 방향으로 세팅되므로 실제 발사 방향은 반대
        Vector3 firingDir = -_podPitch.forward;

        // 카메라 위치: 발사 방향 뒤쪽에서 위로 올려서 튜브 앞을 내려다봄
        Vector3 camPos = _podPitch.position
                       - firingDir * 2.5f
                       + Vector3.up * 1.5f;
        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position, camPos, Time.deltaTime * smoothing);

        // 카메라 회전: 실제 발사 방향을 바라봄
        if (firingDir.sqrMagnitude > 0.01f)
            _cam.transform.rotation = Quaternion.Slerp(
                _cam.transform.rotation,
                Quaternion.LookRotation(firingDir, Vector3.up),
                Time.deltaTime * smoothing);
    }

    void OnGUI()
    {
        int sw = Screen.width;
        int sh = Screen.height;

        int x = Mathf.RoundToInt(pipX * sw);
        int y = Mathf.RoundToInt((1f - pipY - pipHeight) * sh);
        int w = Mathf.RoundToInt(pipWidth  * sw);
        int h = Mathf.RoundToInt(pipHeight * sh);

        // ── 테두리 ────────────────────────────────────────
        GUI.color = new Color(0f, 1f, 0.3f, 0.95f);
        GUI.DrawTexture(new Rect(x - 2,  y - 2,  w + 4, 2),     _whiteTex);
        GUI.DrawTexture(new Rect(x - 2,  y + h,  w + 4, 2),     _whiteTex);
        GUI.DrawTexture(new Rect(x - 2,  y - 2,  2,     h + 4), _whiteTex);
        GUI.DrawTexture(new Rect(x + w,  y - 2,  2,     h + 4), _whiteTex);

        // ── 라벨 ─────────────────────────────────────────
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize  = 11;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = new Color(0f, 1f, 0.3f, 0.95f);
        GUI.Label(new Rect(x + 4, y + 4, 140, 18), "AIM VIEW", style);

        // 타겟까지 거리 표시
        GameObject[] tgts = GameObject.FindGameObjectsWithTag("Target");
        if (tgts.Length > 0 && _podPitch != null)
        {
            float minDist = float.MaxValue;
            foreach (var t in tgts)
            {
                float d = Vector3.Distance(_podPitch.position, t.transform.position);
                if (d < minDist) minDist = d;
            }
            style.normal.textColor = new Color(1f, 0.9f, 0.1f, 1f);
            GUI.Label(new Rect(x + 4, y + 18, 160, 18),
                      $"RANGE: {minDist:F1} m", style);
        }

        // ── 조준선 (크로스헤어) ───────────────────────────
        int cx = x + w / 2;
        int cy = y + h / 2;
        int cs = 10;   // 조준선 반지름
        int cg = 3;    // 중앙 갭

        GUI.color = new Color(0f, 1f, 0.3f, 0.9f);
        // 가로
        GUI.DrawTexture(new Rect(cx - cs, cy - 1, cs - cg,     2), _whiteTex);
        GUI.DrawTexture(new Rect(cx + cg, cy - 1, cs - cg,     2), _whiteTex);
        // 세로
        GUI.DrawTexture(new Rect(cx - 1,  cy - cs, 2, cs - cg),    _whiteTex);
        GUI.DrawTexture(new Rect(cx - 1,  cy + cg, 2, cs - cg),    _whiteTex);
        // 중앙 점
        GUI.DrawTexture(new Rect(cx - 1,  cy - 1,  3, 3),           _whiteTex);

        GUI.color = Color.white;
    }

    void OnDestroy()
    {
        if (_whiteTex != null) Destroy(_whiteTex);
    }
}
