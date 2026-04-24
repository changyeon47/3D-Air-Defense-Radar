/*
 * OverviewCamera.cs
 * 발사대 + 타겟을 함께 보여주는 오버뷰 카메라.
 * 화면 우하단에 작은 PIP(Picture-in-Picture)로 표시된다.
 *
 * 사용법:
 *   - 빈 GameObject에 이 스크립트 추가
 */

using UnityEngine;

public class OverviewCamera : MonoBehaviour
{
    [Header("PIP 위치 & 크기 (0~1 비율)")]
    [Tooltip("화면 오른쪽 끝 기준 X 위치")]
    public float pipX      = 0.68f;
    [Tooltip("화면 아래쪽 기준 Y 위치")]
    public float pipY      = 0.02f;
    [Tooltip("PIP 가로 크기")]
    public float pipWidth  = 0.30f;
    [Tooltip("PIP 세로 크기")]
    public float pipHeight = 0.26f;

    [Header("카메라 설정")]
    [Tooltip("카메라 옆 거리 (발사대 기준)")]
    public float sideDistance  = 30f;
    [Tooltip("카메라 높이")]
    public float camHeight     = 5f;
    [Tooltip("최소 FOV")]
    public float minFov        = 30f;
    [Tooltip("최대 FOV")]
    public float maxFov        = 80f;
    [Tooltip("FOV 보간 속도")]
    public float fovSmoothing  = 2f;
    [Tooltip("위치 보간 속도")]
    public float posSmoothing  = 3f;

    private Camera _cam;
    private float  _targetFov;

    void Awake()
    {
        GameObject camObj = new GameObject("OverviewCam");
        _cam = camObj.AddComponent<Camera>();

        _cam.rect            = new Rect(pipX, pipY, pipWidth, pipHeight);
        _cam.depth           = 1;
        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.1f, 0.12f, 0.1f);
        _cam.farClipPlane    = 500f;

        // 초기 위치: 발사대 옆 아래쪽에서 비스듬히
        camObj.transform.position    = new Vector3(sideDistance, camHeight, 0f);
        camObj.transform.eulerAngles = new Vector3(10f, -90f, 0f);

        _targetFov = _cam.fieldOfView = 50f;
    }

    void LateUpdate()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag("Target");

        Vector3 launcherPos = Vector3.zero;
        THAADLauncher launcher = FindFirstObjectByType<THAADLauncher>();
        if (launcher != null) launcherPos = launcher.transform.position;

        // 발사대 + 타겟 중심 계산
        Vector3 center = launcherPos;
        float maxDist  = 0f;

        foreach (var t in targets)
        {
            center += t.transform.position;
            float d = Vector3.Distance(launcherPos, t.transform.position);
            if (d > maxDist) maxDist = d;
        }
        center /= (targets.Length + 1);

        // 카메라를 발사대 옆 아래쪽에 고정, 중심 높이만 따라감
        Vector3 desiredPos = new Vector3(
            launcherPos.x + sideDistance,
            launcherPos.y + camHeight,
            center.z);

        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position, desiredPos, Time.deltaTime * posSmoothing);

        // 카메라 시야에서 벗어난 오브젝트가 있으면 FOV 자동 확대
        float requiredHalfFov = minFov * 0.5f;
        Vector3 camPos = _cam.transform.position;
        Vector3 camFwd = _cam.transform.forward;

        // 발사대 포함 각도 계산
        float aLauncher = Vector3.Angle(camFwd, (launcherPos - camPos).normalized);
        requiredHalfFov = Mathf.Max(requiredHalfFov, aLauncher + 8f);

        // 모든 타겟의 각도 계산 — 벗어난 게 있으면 FOV 확대
        foreach (var t in targets)
        {
            float a = Vector3.Angle(camFwd, (t.transform.position - camPos).normalized);
            requiredHalfFov = Mathf.Max(requiredHalfFov, a + 8f);
        }

        _targetFov = Mathf.Clamp(requiredHalfFov * 2f, minFov, maxFov);
        _cam.fieldOfView = Mathf.Lerp(
            _cam.fieldOfView, _targetFov, Time.deltaTime * fovSmoothing);

        // 발사대와 타겟 중간 지점을 바라봄
        Vector3 lookTarget = new Vector3(launcherPos.x, center.y * 0.5f + launcherPos.y * 0.5f, center.z);
        Vector3 dir = (lookTarget - _cam.transform.position).normalized;
        if (dir != Vector3.zero)
            _cam.transform.rotation = Quaternion.Lerp(
                _cam.transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up),
                Time.deltaTime * posSmoothing);
    }

    void OnGUI()
    {
        // PIP 테두리 & 라벨
        int sw = Screen.width;
        int sh = Screen.height;

        int x = Mathf.RoundToInt(pipX * sw);
        int y = Mathf.RoundToInt((1f - pipY - pipHeight) * sh);
        int w = Mathf.RoundToInt(pipWidth  * sw);
        int h = Mathf.RoundToInt(pipHeight * sh);

        // 테두리
        GUI.color = new Color(0f, 0.8f, 0f, 0.9f);
        GUI.DrawTexture(new Rect(x - 2,     y - 2,     w + 4, 2),     Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 2,     y + h,     w + 4, 2),     Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 2,     y - 2,     2,     h + 4), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + w,     y - 2,     2,     h + 4), Texture2D.whiteTexture);

        // 라벨
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize  = 11;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = new Color(0f, 1f, 0f, 0.9f);
        GUI.Label(new Rect(x + 4, y + 4, 120, 18), "OVERVIEW", style);

        // 타겟 수
        int targetCount = GameObject.FindGameObjectsWithTag("Target").Length;
        style.normal.textColor = targetCount > 0
            ? new Color(1f, 0.2f, 0.2f, 1f)
            : new Color(0f, 1f, 0f, 0.7f);
        GUI.Label(new Rect(x + 4, y + 18, 120, 18),
                  $"TARGETS: {targetCount}", style);

        GUI.color = Color.white;
    }
}
