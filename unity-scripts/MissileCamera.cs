/*
 * MissileCamera.cs
 * 미사일 뒤에서 따라가는 3인칭 카메라.
 */

using UnityEngine;

public class MissileCamera : MonoBehaviour
{
    [Header("카메라 오프셋")]
    public float behindDistance = 8f;   // 미사일 뒤 거리
    public float aboveHeight    = 2.5f; // 위 높이
    public float smoothSpeed    = 5f;   // 따라가는 속도

    [Header("폭발 후 감상 시간")]
    public float watchTime = 1.5f;

    [Header("미사일 추적 최대 시간")]
    [Tooltip("발사 후 이 시간(초)이 지나면 발사대 뷰로 복귀")]
    public float maxFollowTime = 3f;

    [Header("기본 위치")]
    public Vector3 defaultPosition = new Vector3(-8f, 8f, -12f);
    public Vector3 defaultRotation = new Vector3(19f, 34f, 0f);

    private Transform _target;
    private Rigidbody _targetRb;
    private bool      _watching;
    private Vector3   _explodePos;
    private float     _watchTimer;
    private float     _followTimer;

    // 런처 감상 모드
    private Transform _launcherWatch;

    void Awake()
    {
        SnapToDefault();
    }

    void Start()
    {
        // SceneSetup Awake 이후에도 한 번 더 고정
        SnapToDefault();
    }

    void SnapToDefault()
    {
        transform.position = defaultPosition;
        transform.rotation = Quaternion.Euler(defaultRotation);

        // 배경이 검정이 되지 않도록 SolidColor 폴백 설정
        Camera cam = GetComponent<Camera>();
        if (cam != null && cam.clearFlags == CameraClearFlags.Nothing)
        {
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.38f, 0.60f, 0.85f);
        }
    }

    // 런처 감상 시작 (포드 회전 중)
    public void WatchLauncher(Transform launcher)
    {
        _launcherWatch = launcher;
        _target        = null;
        _watching      = false;
    }

    public void SetTarget(Transform t, Rigidbody rb)
    {
        _launcherWatch = null;
        _target        = t;
        _targetRb      = rb;
        _watching      = false;
        _followTimer   = 0f;    // 추적 타이머 리셋

        // 등록 즉시 카메라를 뒤+위로 순간이동
        if (t != null)
        {
            Vector3 flyDir = (rb != null && rb.linearVelocity.magnitude > 0.1f)
                ? rb.linearVelocity.normalized
                : t.up;

            // 항상 위쪽 성분을 보정해서 절대 아래에서 보이지 않게
            Vector3 camPos = t.position
                           - flyDir * behindDistance
                           + Vector3.up * Mathf.Max(aboveHeight, 2f);

            transform.position = camPos;
            // 미사일 바라보도록 즉시 회전
            Vector3 lookDir = (t.position - camPos).normalized;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        }
    }

    public void OnMissileExplode(Vector3 pos)
    {
        _target     = null;
        _targetRb   = null;
        _explodePos = pos;
        _watching   = true;
        _watchTimer = 0f;
    }

    void LateUpdate()
    {
        // ── 런처 포드 회전 감상 ──────────────────────────────
        if (_launcherWatch != null && _target == null && !_watching)
        {
            // 런처 뒤쪽 대각선 위에서 포드를 내려다보는 시점
            Vector3 launcherViewPos = _launcherWatch.position + new Vector3(-8f, 8f, -12f);
            transform.position = Vector3.Lerp(transform.position, launcherViewPos, Time.deltaTime * 3f);

            // 포드 중심 바라보기
            Vector3 podCenter = _launcherWatch.position + new Vector3(0f, 3f, 0f);
            Vector3 dir = (podCenter - transform.position).normalized;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Lerp(transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * 4f);
            return;
        }

        // 폭발 지점 감상
        if (_watching)
        {
            _watchTimer += Time.deltaTime;
            Vector3 dir = (_explodePos - transform.position).normalized;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Lerp(transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * 6f);

            if (_watchTimer >= watchTime) _watching = false;
            return;
        }

        // 미사일 없으면 기본 위치
        if (_target == null)
        {
            transform.position = Vector3.Lerp(transform.position, defaultPosition, Time.deltaTime * 3f);
            transform.rotation = Quaternion.Lerp(transform.rotation,
                Quaternion.Euler(defaultRotation), Time.deltaTime * 3f);
            return;
        }

        // 최대 추적 시간 초과 시 발사대 뷰로 복귀
        _followTimer += Time.deltaTime;
        if (_followTimer >= maxFollowTime)
        {
            _target   = null;
            _targetRb = null;
            return;
        }

        // 속도 방향 기준으로 뒤+위 위치 계산
        Vector3 flyDir = (_targetRb != null && _targetRb.linearVelocity.magnitude > 0.1f)
            ? _targetRb.linearVelocity.normalized
            : _target.up;

        Vector3 desiredPos = _target.position
                           - flyDir * behindDistance
                           + Vector3.up * aboveHeight;

        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothSpeed);

        // 미사일 바라보기
        Vector3 lookDir = (_target.position - transform.position).normalized;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.Lerp(transform.rotation,
                Quaternion.LookRotation(lookDir, Vector3.up), Time.deltaTime * smoothSpeed);
    }
}
