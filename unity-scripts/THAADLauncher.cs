/*
 * THAADLauncher.cs
 * THAAD 스타일 발사대.
 * - 대형 군용 트럭 + 미사일 포드 (2x3 = 6발)
 * - 발사 시 포드가 타겟 방향으로 회전 후 발사
 */

using UnityEngine;
using System.Collections;

public class THAADLauncher : MonoBehaviour
{
    [Header("미사일 설정")]
    public GameObject missilePrefab;
    public float      fireCooldown   = 2.5f;
    public float      rotateSpeed    = 60f;    // 포드 회전 속도 (도/초, FireWhenAimed 판정용)
    public float      trackSmoothing = 6f;     // 추적 보간 속도 (높을수록 빠름)

    [Header("발사대 각도")]
    [Range(50f, 100f)]
    public float launchAngle = 95f;


    [Header("발사 조건")]
    [Tooltip("타겟 감지 후 이 시간(초) 동안 추적한 뒤 발사")]
    public float trackingDelay = 2.0f;
    [Tooltip("미사일 예상 속도 (예측 조준용, MissileController thrustForce 참고)")]
    public float missileSpeed = 22f;
    [Tooltip("속도 평활화 계수 (낮을수록 부드럽게)")]
    public float velocitySmoothing = 0.15f;

    // 포드 피벗 (회전 기준)
    private Transform _podPivot;
    private Transform _podYaw;   // 좌우 회전
    private Transform _podPitch; // 위아래 회전

    // 미사일 발사 위치 & TopCap (AimCamera 기준)
    private Transform[] _tubes = new Transform[6];
    private Transform   _topCap;
    private int   _nextTube   = 0;
    private float _lastFire   = -999f;
    private bool  _isRotating = false;

    // 타겟 추적 & 예측
    private Vector3    _lastTargetPos;
    private Vector3    _smoothedVelocity;
    private float      _trackStartTime = -999f;
    private GameObject _trackedTarget;

    // 머티리얼
    private Material _oliveMat, _darkMat, _tireMat, _glassMat, _tubeMat, _silverMat;

    void Awake()
    {
        transform.localScale = Vector3.one * 2f;  // 런처 크기 2배
        CreateMaterials();
        BuildTruck();
        BuildPodMount();
        BuildLauncherPod();
        BuildSupportLegs();
    }

    void CreateMaterials()
    {
        _oliveMat  = Mat(new Color(0.22f, 0.26f, 0.16f), 0.2f, 0.15f);
        _darkMat   = Mat(new Color(0.12f, 0.12f, 0.10f), 0.7f, 0.3f);
        _tireMat   = Mat(new Color(0.07f, 0.07f, 0.07f), 0.1f, 0.05f);
        _glassMat  = Mat(new Color(0.35f, 0.55f, 0.65f), 0.9f, 0.9f);
        _tubeMat   = Mat(new Color(0.18f, 0.20f, 0.13f), 0.5f, 0.2f);
        _silverMat = Mat(new Color(0.45f, 0.45f, 0.42f), 0.9f, 0.6f);
    }

    // ── 트럭 본체 ─────────────────────────────────────────
    void BuildTruck()
    {
        // 메인 프레임
        Box("Frame",      new Vector3(0f,   0.55f,  0f),   new Vector3(2.6f, 0.4f, 9.0f), _oliveMat);
        Box("FrameSide1", new Vector3( 1.2f, 0.55f, 0f),   new Vector3(0.1f, 0.5f, 9.0f), _darkMat);
        Box("FrameSide2", new Vector3(-1.2f, 0.55f, 0f),   new Vector3(0.1f, 0.5f, 9.0f), _darkMat);

        // 운전석 캐빈
        Box("Cabin",      new Vector3(0f,  1.55f,  3.2f),  new Vector3(2.5f, 1.6f, 2.4f), _oliveMat);
        Box("CabinRoof",  new Vector3(0f,  2.38f,  3.2f),  new Vector3(2.3f, 0.12f,2.2f), _darkMat);
        Box("CabinVisor", new Vector3(0f,  2.28f,  4.3f),  new Vector3(2.2f, 0.12f,0.5f), _darkMat);

        // 앞 그릴 + 범퍼
        Box("Grille",  new Vector3(0f, 1.15f, 4.35f), new Vector3(2.2f, 1.0f, 0.12f), _darkMat);
        Box("Bumper",  new Vector3(0f, 0.52f, 4.4f),  new Vector3(2.6f, 0.28f,0.18f), _silverMat);
        Box("Bumper2", new Vector3(0f, 0.35f, 4.35f), new Vector3(2.6f, 0.12f,0.35f), _silverMat);

        // 윈드실드
        Box("Wind",   new Vector3(0f, 1.82f, 4.33f), new Vector3(1.9f, 0.65f,0.05f), _glassMat);
        Box("WinL",   new Vector3( 1.22f,1.82f,3.2f),new Vector3(0.05f,0.55f,1.6f), _glassMat);
        Box("WinR",   new Vector3(-1.22f,1.82f,3.2f),new Vector3(0.05f,0.55f,1.6f), _glassMat);

        // 사이드 미러
        Box("MirrorL", new Vector3( 1.45f,2.0f,4.1f), new Vector3(0.25f,0.15f,0.08f), _darkMat);
        Box("MirrorR", new Vector3(-1.45f,2.0f,4.1f), new Vector3(0.25f,0.15f,0.08f), _darkMat);

        // 배기관
        Cyl("Exhaust", new Vector3(1.4f,2.3f,2.2f), new Vector3(0.09f,0.9f,0.09f), _darkMat);

        // 헤드라이트
        EmissiveBox("HeadL", new Vector3( 0.85f,1.05f,4.38f), new Vector3(0.35f,0.22f,0.06f), new Color(1f,1f,0.8f));
        EmissiveBox("HeadR", new Vector3(-0.85f,1.05f,4.38f), new Vector3(0.35f,0.22f,0.06f), new Color(1f,1f,0.8f));

        // 후미등
        EmissiveBox("TailL", new Vector3( 1.1f,0.9f,-4.45f), new Vector3(0.25f,0.18f,0.06f), new Color(1f,0.1f,0.05f));
        EmissiveBox("TailR", new Vector3(-1.1f,0.9f,-4.45f), new Vector3(0.25f,0.18f,0.06f), new Color(1f,0.1f,0.05f));

        // 8개 바퀴
        float[] wx = { 1.55f,-1.55f, 1.55f,-1.55f, 1.55f,-1.55f, 1.55f,-1.55f };
        float[] wz = { 3.2f,  3.2f,  1.2f,  1.2f, -1.4f, -1.4f, -3.2f, -3.2f };
        for (int i = 0; i < 8; i++) Wheel(new Vector3(wx[i], 0.08f, wz[i]));

        // 액슬
        Box("Axle1",new Vector3(0f,0.22f, 3.2f),new Vector3(3.3f,0.14f,0.22f),_darkMat);
        Box("Axle2",new Vector3(0f,0.22f, 1.2f),new Vector3(3.3f,0.14f,0.22f),_darkMat);
        Box("Axle3",new Vector3(0f,0.22f,-1.4f),new Vector3(3.3f,0.14f,0.22f),_darkMat);
        Box("Axle4",new Vector3(0f,0.22f,-3.2f),new Vector3(3.3f,0.14f,0.22f),_darkMat);

        // 연료통
        Box("Tank1",new Vector3( 1.35f,0.8f,-0.5f),new Vector3(0.25f,0.6f,1.8f),_darkMat);
        Box("Tank2",new Vector3(-1.35f,0.8f,-0.5f),new Vector3(0.25f,0.6f,1.8f),_darkMat);
    }

    void Wheel(Vector3 pos)
    {
        GameObject w = Cyl("Wheel", pos, new Vector3(0.62f,0.28f,0.62f), _tireMat);
        w.transform.localEulerAngles = new Vector3(0f,0f,90f);
        GameObject hub = Cyl("Hub", Vector3.zero, new Vector3(0.5f,0.32f,0.5f), _silverMat);
        hub.transform.SetParent(w.transform,false);
        hub.transform.localPosition = new Vector3(0f,1.02f,0f);
        hub.transform.localScale    = new Vector3(0.5f,0.08f,0.5f);
    }

    // ── 포드 마운트 (좌우+상하 회전) ─────────────────────
    void BuildPodMount()
    {
        // 마운트 베이스 (트럭 적재함 뒤쪽)
        Box("MountBase", new Vector3(0f,1.0f,-2.5f), new Vector3(2.0f,0.5f,2.0f), _darkMat);
        Box("MountTop",  new Vector3(0f,1.3f,-2.5f), new Vector3(1.6f,0.2f,1.6f), _silverMat);

        // 좌우 회전축
        _podYaw = new GameObject("PodYaw").transform;
        _podYaw.SetParent(transform, false);
        _podYaw.localPosition = new Vector3(0f, 1.4f, -2.5f);

        // 상하 회전축
        _podPitch = new GameObject("PodPitch").transform;
        _podPitch.SetParent(_podYaw, false);
        _podPitch.localPosition    = Vector3.zero;
        _podPitch.localEulerAngles = new Vector3(-(90f - launchAngle), 0f, 0f);

        // 피벗 포인트 참조 저장
        _podPivot = _podPitch;

        // 힌지 원통
        Cyl_Child(_podYaw, "HingeL", new Vector3( 1.1f,0f,0f), new Vector3(0.25f,0.6f,0.25f), _silverMat,
                  new Vector3(0f,0f,90f));
        Cyl_Child(_podYaw, "HingeR", new Vector3(-1.1f,0f,0f), new Vector3(0.25f,0.6f,0.25f), _silverMat,
                  new Vector3(0f,0f,90f));
    }

    // ── 미사일 포드 ──────────────────────────────────────
    void BuildLauncherPod()
    {
        // 포드 외장
        BoxChild(_podPivot,"Pod",      new Vector3(0f,2.0f,0f), new Vector3(2.2f,4.2f,1.1f), _tubeMat);
        BoxChild(_podPivot,"PodBack",  new Vector3(0f,2.0f,-0.56f),new Vector3(2.2f,4.2f,0.06f),_darkMat);
        BoxChild(_podPivot,"PodFront", new Vector3(0f,2.0f, 0.56f),new Vector3(2.2f,4.2f,0.06f),_oliveMat);

        // 보강 리브
        for (int i = 0; i < 4; i++)
            BoxChild(_podPivot,$"Rib{i}", new Vector3(0f,0.4f+i*1.2f,0f), new Vector3(2.3f,0.07f,1.15f),_silverMat);

        // 측면 보강판
        BoxChild(_podPivot,"SideL",new Vector3( 1.12f,2.0f,0f),new Vector3(0.08f,4.2f,1.1f),_darkMat);
        BoxChild(_podPivot,"SideR",new Vector3(-1.12f,2.0f,0f),new Vector3(0.08f,4.2f,1.1f),_darkMat);

        // 상단 캡 (AimCamera 기준점)
        GameObject topCapGO = BoxChild(_podPivot,"TopCap",new Vector3(0f,4.3f,0f),new Vector3(2.2f,0.2f,1.1f),_silverMat);
        _topCap = topCapGO.transform;

        // 미사일 튜브 2열 3행
        float[] xs = { 0.58f, -0.58f };
        float[] ys = { 0.55f,  1.85f,  3.15f };
        int idx = 0;
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 2; col++)
        {
            MissileTube(_podPivot, new Vector3(xs[col], ys[row], 0f), idx);
            idx++;
        }
    }

    void MissileTube(Transform parent, Vector3 lPos, int idx)
    {
        // 튜브 외통
        GameObject tube = Cyl_Child(parent,$"TubeOut{idx}",lPos,
                          new Vector3(0.42f,0.56f,0.42f),_darkMat, new Vector3(90f,0f,0f));

        // 내부 그림자 효과 (포드 -Z면 = 발사 방향)
        Cyl_Child(parent,$"TubeIn{idx}",lPos+new Vector3(0f,0f,-0.5f),
                  new Vector3(0.36f,0.04f,0.36f),Mat(new Color(0.05f,0.05f,0.05f),0f,0f),
                  new Vector3(90f,0f,0f));

        // 튜브 앞 링
        Cyl_Child(parent,$"TubeRing{idx}",lPos+new Vector3(0f,0f,-0.54f),
                  new Vector3(0.46f,0.04f,0.46f),_silverMat, new Vector3(90f,0f,0f));

        // 발사 위치 (-Z면, 포드가 타겟 방향으로 향하는 쪽)
        GameObject lp = new GameObject($"LaunchPoint_{idx}");
        lp.transform.SetParent(parent,false);
        lp.transform.localPosition    = lPos + new Vector3(0f,0f,-0.7f);
        lp.transform.localEulerAngles = new Vector3(0f,0f,0f);
        if (idx < _tubes.Length) _tubes[idx] = lp.transform;

        // H70 미사일 튜브에 배치 (정적)
        if (missilePrefab != null)
        {
            GameObject m = Instantiate(missilePrefab);
            m.transform.SetParent(parent,false);
            m.transform.localPosition    = lPos;
            m.transform.localEulerAngles = new Vector3(0f,0f,0f);
            m.transform.localScale       = Vector3.one * 0.75f;
            MissileController mc = m.GetComponent<MissileController>();
            if (mc != null)
            {
                mc.CancelInvoke();       // 폭발 타이머 취소
                mc.StopAllCoroutines();  // 카메라 등록 코루틴 취소
                mc.enabled = false;
            }
            Rigidbody rb = m.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
        }
    }

    // ── 유압 지지대 ──────────────────────────────────────
    void BuildSupportLegs()
    {
        Vector3[] pos = {
            new Vector3( 1.8f,0f, 3.0f), new Vector3(-1.8f,0f, 3.0f),
            new Vector3( 1.8f,0f,-3.8f), new Vector3(-1.8f,0f,-3.8f)
        };
        float[] rx = { -15f,-15f, 15f, 15f };
        float[] rz = {  12f,-12f, 12f,-12f };

        for (int i = 0; i < 4; i++)
        {
            GameObject leg = Cyl("Leg"+i, pos[i]+new Vector3(0f,-0.1f,0f),
                             new Vector3(0.16f,0.55f,0.16f), _darkMat);
            leg.transform.localEulerAngles = new Vector3(rx[i],0f,rz[i]);

            GameObject foot = Box("Foot"+i, pos[i]+new Vector3(rz[i]>0?0.15f:-0.15f,-0.55f,rx[i]<0?0.1f:-0.1f),
                              new Vector3(0.55f,0.07f,0.55f), _darkMat);
        }
    }

    // ── 자동 추적 + 스페이스바 발사 ──────────────────────
    void Update()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag("Target");
        if (targets.Length == 0)
        {
            _trackedTarget    = null;
            _smoothedVelocity = Vector3.zero;
            _trackStartTime   = -999f;
            return;
        }

        // ── 타겟 선택: Sticky Lock-On ──────────────────────
        // 현재 추적 중인 타겟이 아직 살아있으면 그대로 유지 (락온 유지)
        // 사라진 경우에만 가장 가까운 타겟으로 전환
        GameObject nearest = null;
        if (_trackedTarget != null)
        {
            foreach (var t in targets)
                if (t == _trackedTarget) { nearest = t; break; }
        }
        if (nearest == null)
        {
            float minD = float.MaxValue;
            foreach (var t in targets)
            {
                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d < minD) { minD = d; nearest = t; }
            }
        }

        // 타겟이 바뀌면 추적 초기화
        if (nearest != _trackedTarget)
        {
            _trackedTarget    = nearest;
            _lastTargetPos    = nearest.transform.position;
            _smoothedVelocity = Vector3.zero;
            _trackStartTime   = Time.time;
        }

        // 속도 평활화 (지수 이동 평균)
        Vector3 rawVelocity = (nearest.transform.position - _lastTargetPos) / Time.deltaTime;
        _smoothedVelocity   = Vector3.Lerp(_smoothedVelocity, rawVelocity, velocitySmoothing);
        _lastTargetPos      = nearest.transform.position;

        // 포드는 항상 타겟을 자동 추적
        TrackTarget(nearest.transform.position);

        // 발사: 스페이스바 + 쿨타임 완료 + 회전 중 아닐 때
        if (Input.GetKeyDown(KeyCode.Space) &&
            !_isRotating &&
            Time.time - _lastFire >= fireCooldown)
        {
            StartCoroutine(FireWhenAimed(nearest));
        }
    }

    // 미사일 비행 시간을 고려한 예측 위치 계산
    Vector3 PredictPosition(Vector3 currentPos)
    {
        float dist         = Vector3.Distance(_podYaw != null ? _podYaw.position : transform.position, currentPos);
        float flightTime   = dist / Mathf.Max(missileSpeed, 1f);
        return currentPos + _smoothedVelocity * flightTime;
    }

    // 포드를 지정 위치 방향으로 추적
    void TrackTarget(Vector3 targetPos)
    {
        if (_podYaw == null || _podPitch == null) return;

        Vector3 toTarget = targetPos - _podYaw.position;

        // ── Yaw: _podYaw.forward가 타겟 반대 → 포드 -Z면이 타겟을 향함 ─
        float flatDist = Mathf.Sqrt(toTarget.x * toTarget.x + toTarget.z * toTarget.z);
        if (flatDist > 0.01f)
        {
            Quaternion yawTgt = Quaternion.LookRotation(
                -new Vector3(toTarget.x, 0f, toTarget.z).normalized, Vector3.up);
            _podYaw.rotation = Quaternion.Slerp(
                _podYaw.rotation, yawTgt, Time.deltaTime * trackSmoothing);
        }

        // ── Pitch: 꼭대기(+Y)가 타겟 고도를 정확히 가리키려면 elev-90° ──
        float elev = Mathf.Atan2(toTarget.y, Mathf.Max(flatDist, 0.1f)) * Mathf.Rad2Deg;
        elev = Mathf.Clamp(elev, 0f, 82f);
        float podAngle = Mathf.Clamp(elev - 90f, -85f, 0f);

        Quaternion pitchTgt = Quaternion.Euler(podAngle, 0f, 0f);
        _podPitch.localRotation = Quaternion.Slerp(
            _podPitch.localRotation, pitchTgt, Time.deltaTime * trackSmoothing);
    }

    IEnumerator FireWhenAimed(GameObject target)
    {
        _isRotating = true;

        // yaw가 타겟 수평 방향에 8° 이내로 수렴하면 발사
        while (target != null)
        {
            Vector3 toTarget      = target.transform.position - _podYaw.position;
            Vector3 flatToTarget  = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            float   yawAngle      = Vector3.Angle(-_podYaw.forward, flatToTarget);
            if (yawAngle < 8f) break;
            yield return null;
        }

        if (target != null)
        {
            Fire();
            _lastFire       = Time.time;
            _trackStartTime = Time.time; // 발사 후 다시 추적 타이머 리셋
        }
        _isRotating = false;
    }

    void Fire()
    {
        if (missilePrefab == null) return;
        if (_nextTube >= _tubes.Length) _nextTube = 0;

        Transform tube = _tubes[_nextTube];
        if (tube == null) return;

        // 실제 튜브 출구에서 발사 (포드 -Z면 = 타겟 방향)
        Vector3 spawnPos = tube.position;

        // 포드 -Z가 발사 방향 (yaw negation 사용)
        Vector3 firingDir = -_podPitch.forward;
        if (_trackedTarget != null)
        {
            Vector3 toTarget = (_trackedTarget.transform.position - spawnPos).normalized;
            if (toTarget != Vector3.zero) firingDir = toTarget;
        }
        Quaternion spawnRot = Quaternion.LookRotation(firingDir, Vector3.up);

        GameObject missile = Instantiate(missilePrefab, spawnPos, spawnRot);
        Destroy(missile, 12f);
        _nextTube++;

        // 레이더 발사 이펙트 트리거
        RadarDisplay radar = FindFirstObjectByType<RadarDisplay>();
        if (radar != null) radar.OnFired();
    }

    // AimCamera에서 포드 피치 방향 참조용
    public Transform GetPodPitch() => _podPitch;

    // AimCamera TopCap 기준점 참조용
    public Transform GetTopCap() => _topCap;

    // AimCamera에서 현재 발사 튜브 위치 참조용
    public Transform GetNextTube()
    {
        if (_tubes == null || _tubes.Length == 0) return null;
        return _tubes[_nextTube % _tubes.Length];
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    GameObject Box(string n, Vector3 lp, Vector3 ls, Material m)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name=n; o.transform.SetParent(transform,false);
        o.transform.localPosition=lp; o.transform.localScale=ls;
        o.GetComponent<Renderer>().material=m;
        Destroy(o.GetComponent<Collider>()); return o;
    }
    GameObject BoxChild(Transform p,string n,Vector3 lp,Vector3 ls,Material m)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name=n; o.transform.SetParent(p,false);
        o.transform.localPosition=lp; o.transform.localScale=ls;
        o.GetComponent<Renderer>().material=m;
        Destroy(o.GetComponent<Collider>()); return o;
    }
    GameObject Cyl(string n,Vector3 lp,Vector3 ls,Material m)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        o.name=n; o.transform.SetParent(transform,false);
        o.transform.localPosition=lp; o.transform.localScale=ls;
        o.GetComponent<Renderer>().material=m;
        Destroy(o.GetComponent<Collider>()); return o;
    }
    GameObject Cyl_Child(Transform p,string n,Vector3 lp,Vector3 ls,Material m,Vector3 euler)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        o.name=n; o.transform.SetParent(p,false);
        o.transform.localPosition=lp; o.transform.localScale=ls;
        o.transform.localEulerAngles=euler;
        o.GetComponent<Renderer>().material=m;
        Destroy(o.GetComponent<Collider>()); return o;
    }
    void EmissiveBox(string n,Vector3 lp,Vector3 ls,Color emit)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name=n; o.transform.SetParent(transform,false);
        o.transform.localPosition=lp; o.transform.localScale=ls;
        Material m=new Material(Shader.Find("Standard"));
        m.color=emit; m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor",emit*1.5f);
        o.GetComponent<Renderer>().material=m;
        Destroy(o.GetComponent<Collider>());
    }
    Material Mat(Color c,float met,float glos)
    {
        Material m=new Material(Shader.Find("Standard"));
        m.color=c; m.SetFloat("_Metallic",met); m.SetFloat("_Glossiness",glos);
        return m;
    }
}
