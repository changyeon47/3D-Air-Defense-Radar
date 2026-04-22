/*
 * MissileController.cs
 * 미사일 외형(동체+노즈콘+날개)을 프리미티브로 생성하고
 * 연기 트레일, 엔진 불빛, 폭발 효과를 처리한다.
 *
 * 사용법:
 *   - 완전히 빈 GameObject(Empty)에 이 스크립트 추가
 *   - Sphere 프리미티브가 아닌 Create Empty로 만들어야 함
 */

using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class MissileController : MonoBehaviour
{
    [Header("비행 설정")]
    public float thrustForce = 25f;   // 추진력
    public float lifetime    = 6f;    // 최대 비행 시간

    [Header("트레일 설정")]
    public float trailTime  = 0.6f;
    public float trailWidth = 0.12f;

    private Rigidbody     _rb;
    private Light         _engineLight;
    private float         _flicker;
    private bool          _exploded = false;
    private GameObject    _noseTriggerObj;

    // 미사일 길이 (Inspector에서 조정 가능)
    [Header("노즈 오프셋 (모델에 맞게 조정)")]
    public float noseOffset = 1.0f;

    void Start()
    {
        ROSConnection.GetOrCreateInstance().RegisterPublisher<PointMsg>("/target_destroyed");
    }

    void Awake()
    {
        // Rigidbody 설정
        _rb                  = gameObject.AddComponent<Rigidbody>();
        _rb.useGravity        = false;
        _rb.linearDamping     = 0f;
        _rb.angularDamping    = 0f;
        _rb.interpolation     = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // 기존 모델이 없을 때만 프리미티브로 생성
        if (GetComponentsInChildren<MeshRenderer>().Length == 0)
            BuildMissileShape();

        AddTrailRenderer();
        AddEngineLight();

        // H70 기존 콜라이더 전부 비활성화 (몸체 충돌 방지)
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        // 발사 초기 속도: 포드가 겨냥한 방향(transform.forward)으로 발사
        Vector3 launchDir = transform.forward.y > 0.1f
            ? transform.forward          // 포드가 위를 향하면 그 방향 사용
            : Vector3.up;                // 수평 이하면 수직으로 발사
        _rb.linearVelocity = launchDir.normalized * 8f;

        // 노즈가 진행 방향을 즉시 바라보게 회전
        transform.rotation = Quaternion.LookRotation(launchDir.normalized);

        // 노즈 전용 트리거 콜라이더 부착
        AddNoseTrigger();

        // 런처 콜라이더 무시 (뚫고 나가는 현상 방지)
        StartCoroutine(IgnoreLauncherColliders());

        // 카메라에 이 미사일을 추적 대상으로 등록
        // 한 프레임 뒤에 등록해서 초기 위치가 잡힌 후 카메라 이동
        StartCoroutine(RegisterCameraNextFrame());

        Invoke(nameof(ExplodeNoTarget), lifetime);
    }

    void Update()
    {
        _flicker += Time.deltaTime * 25f;
        if (_engineLight != null)
            _engineLight.intensity = 3f + Mathf.Sin(_flicker) * 0.8f;

        // 노즈 트리거를 transform.forward 방향 앞쪽에 위치시킴
        if (_noseTriggerObj != null)
            _noseTriggerObj.transform.position = transform.position + transform.forward * noseOffset;
    }

    void FixedUpdate()
    {
        // 타겟 추적 (폭발은 NoseHitDetector가 담당)
        GameObject target = FindNearestTarget();

        if (target != null)
        {
            // 적까지의 방향 벡터
            Vector3 dir = (target.transform.position - transform.position).normalized;

            // 현재 속도 방향 기준으로 부드럽게 방향 전환
            Vector3 currentVel = _rb.linearVelocity;
            if (currentVel.magnitude > 0.1f)
            {
                // 현재 방향에서 목표 방향으로 서서히 회전 (X,Y,Z 모두 사용)
                Vector3 newDir = Vector3.RotateTowards(
                    currentVel.normalized,
                    dir,
                    Time.fixedDeltaTime * 14f,  // 회전 속도 (라디안/초) — 빠른 타겟 추적
                    0f
                );

                // 속도 크기 유지하면서 방향만 바꿈
                _rb.linearVelocity = newDir * currentVel.magnitude;

                // 노즈가 진행 방향을 즉시 바라보게 회전
                _rb.MoveRotation(Quaternion.LookRotation(newDir));
            }
        }
        else
        {
            // 적 없으면 현재 속도 방향 바라보기
            if (_rb.linearVelocity.magnitude > 0.1f)
                _rb.MoveRotation(Quaternion.LookRotation(_rb.linearVelocity.normalized));
        }

        // 지속 추력 (현재 속도 방향으로)
        if (_rb.linearVelocity.magnitude > 0.1f)
            _rb.AddForce(_rb.linearVelocity.normalized * thrustForce, ForceMode.Acceleration);
    }

    GameObject FindNearestTarget()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag("Target");
        if (targets.Length == 0) return null;

        GameObject nearest = null;
        float minDist = float.MaxValue;

        foreach (GameObject t in targets)
        {
            float dist = Vector3.Distance(transform.position, t.transform.position);
            if (dist < minDist)
            {
                minDist  = dist;
                nearest  = t;
            }
        }
        return nearest;
    }

    // ── 미사일 외형 생성 ──────────────────────────────────
    void BuildMissileShape()
    {
        Material bodyMat = MakeMat(new Color(0.22f, 0.22f, 0.25f), 0.9f, 0.7f);
        Material noseMat = MakeMat(new Color(0.55f, 0.08f, 0.04f), 0.4f, 0.3f);
        Material finMat  = MakeMat(new Color(0.13f, 0.13f, 0.16f), 0.95f, 0.5f);
        Material nozMat  = MakeMat(new Color(0.04f, 0.04f, 0.04f), 1.0f,  0.8f);

        // 동체
        MakePart("Body",   PrimitiveType.Cylinder, Vector3.zero,
                 new Vector3(0.10f, 0.50f, 0.10f), bodyMat);

        // 노즈콘
        MakePart("Nose",   PrimitiveType.Sphere,
                 new Vector3(0f,  0.58f, 0f),
                 new Vector3(0.10f, 0.20f, 0.10f), noseMat);

        // 날개 4개
        MakeFin("Fin_F", new Vector3( 0f,    -0.30f,  0.08f), finMat);
        MakeFin("Fin_B", new Vector3( 0f,    -0.30f, -0.08f), finMat);
        MakeFin("Fin_L", new Vector3(-0.08f, -0.30f,  0f),    finMat);
        MakeFin("Fin_R", new Vector3( 0.08f, -0.30f,  0f),    finMat);

        // 엔진 노즐
        MakePart("Nozzle", PrimitiveType.Cylinder,
                 new Vector3(0f, -0.58f, 0f),
                 new Vector3(0.07f, 0.07f, 0.07f), nozMat);
    }

    void MakePart(string partName, PrimitiveType type,
                  Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(type);
        p.name = partName;
        p.transform.SetParent(transform, false);
        p.transform.localPosition = localPos;
        p.transform.localScale    = localScale;
        p.GetComponent<Renderer>().material = mat;
        // 자식 콜라이더 제거 (루트에 별도 추가)
        Destroy(p.GetComponent<Collider>());
    }

    void MakeFin(string finName, Vector3 localPos, Material mat)
    {
        GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fin.name = finName;
        fin.transform.SetParent(transform, false);
        fin.transform.localPosition = localPos;
        fin.transform.localScale    = new Vector3(0.20f, 0.15f, 0.02f);
        fin.GetComponent<Renderer>().material = mat;
        Destroy(fin.GetComponent<Collider>());
    }

    Material MakeMat(Color color, float metallic, float gloss)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.color = color;
        m.SetFloat("_Metallic",   metallic);
        m.SetFloat("_Glossiness", gloss);
        return m;
    }

    // ── 연기 트레일 ──────────────────────────────────────
    void AddTrailRenderer()
    {
        GameObject trailObj = new GameObject("Trail");
        trailObj.transform.SetParent(transform, false);
        trailObj.transform.localPosition = new Vector3(0f, 0f, -0.60f); // 후면 (-Z)

        TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
        trail.time             = trailTime;
        trail.startWidth       = trailWidth;
        trail.endWidth         = 0.01f;
        trail.minVertexDistance = 0.04f;
        trail.material         = new Material(Shader.Find("Sprites/Default"));

        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.0f, 1.0f, 0.9f), 0.00f), // 흰 불꽃
                new GradientColorKey(new Color(1.0f, 0.45f, 0.1f), 0.15f), // 주황
                new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 0.50f), // 회색 연기
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1.00f), // 짙은 연기
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.00f),
                new GradientAlphaKey(0.9f, 0.15f),
                new GradientAlphaKey(0.5f, 0.50f),
                new GradientAlphaKey(0.0f, 1.00f),
            }
        );
        trail.colorGradient = g;
    }

    // ── 엔진 불빛 ─────────────────────────────────────────
    void AddEngineLight()
    {
        GameObject lo = new GameObject("EngineLight");
        lo.transform.SetParent(transform, false);
        lo.transform.localPosition = new Vector3(0f, 0f, -0.60f); // 후면 (-Z)

        _engineLight           = lo.AddComponent<Light>();
        _engineLight.type      = LightType.Point;
        _engineLight.color     = new Color(1f, 0.5f, 0.1f);
        _engineLight.intensity = 3f;
        _engineLight.range     = 4f;
    }

    // ── 폭발 ─────────────────────────────────────────────
    void Explode(GameObject hitObject = null, Vector3 explodePos = default)
    {
        if (_exploded) return;
        _exploded = true;

        // 폭발 위치: 전달된 위치 없으면 노즈 위치 사용
        if (explodePos == default)
            explodePos = _noseTriggerObj != null
                ? _noseTriggerObj.transform.position
                : transform.position;

        // 폭발 이펙트 생성
        ExplosionEffect.Spawn(explodePos);

        // 카메라에 폭발 위치 전달 (폭발 장면 감상)
        MissileCamera cam = Camera.main?.GetComponent<MissileCamera>();
        if (cam != null) cam.OnMissileExplode(explodePos);

        // 타겟에 맞으면 타겟도 같이 제거 + ROS 퍼블리시
        if (hitObject != null && hitObject.CompareTag("Target"))
        {
            var msg = new PointMsg(explodePos.x, explodePos.y, explodePos.z);
            ROSConnection.GetOrCreateInstance().Publish("/target_destroyed", msg);
            Destroy(hitObject);
        }

        // 폭발 이펙트 오브젝트 — Destroy(gameObject) 전에 반드시 예약 제거
        GameObject fx = new GameObject("ExplosionFX");
        fx.transform.position = explodePos;

        // 밝은 폭발 플래시
        Light flash = fx.AddComponent<Light>();
        flash.type      = LightType.Point;
        flash.color     = new Color(1f, 0.6f, 0.15f);
        flash.intensity = 15f;
        flash.range     = 15f;

        // 폭발 구체
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(fx.transform, false);
        sphere.transform.localScale = Vector3.one * 0.1f;
        Destroy(sphere.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.4f, 0.05f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(2f, 0.8f, 0f));
        sphere.GetComponent<Renderer>().material = mat;

        // fx를 미사일과 무관하게 반드시 제거 (코루틴이 멈춰도 라이트가 남지 않게)
        Destroy(fx, 0.5f);

        Destroy(gameObject);
    }


    // 노즈 전용 트리거 — 속도 방향 기준으로 앞쪽에 배치
    void AddNoseTrigger()
    {
        _noseTriggerObj = new GameObject("NoseTrigger");
        _noseTriggerObj.transform.SetParent(transform, false);

        SphereCollider sc = _noseTriggerObj.AddComponent<SphereCollider>();
        sc.radius    = 0.2f;
        sc.isTrigger = true;

        NoseHitDetector detector = _noseTriggerObj.AddComponent<NoseHitDetector>();
        detector.missile = this;
    }

    // 노즈 트리거 충돌 시 외부에서 호출 (노즈 위치에서 폭발)
    public void OnNoseHit(GameObject hit, Vector3 contactPos)
    {
        if (_exploded) return;
        CancelInvoke(nameof(ExplodeNoTarget));
        Explode(hit, contactPos);
    }

    // 발사 후 1.5초간 런처 콜라이더 무시
    System.Collections.IEnumerator IgnoreLauncherColliders()
    {
        Collider[] myCols = GetComponentsInChildren<Collider>();

        // 씬의 모든 콜라이더 중 런처/발사대 관련만 무시
        Collider[] allCols = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider other in allCols)
        {
            // 타겟 제외하고 자기 자신 제외하고 모두 무시
            if (other.CompareTag("Target")) continue;
            if (other.transform.IsChildOf(transform)) continue;

            foreach (Collider mine in myCols)
                Physics.IgnoreCollision(mine, other, true);
        }

        yield return new WaitForSeconds(1.5f);

        // 1.5초 후 다시 충돌 활성화 (타겟은 계속 감지)
        foreach (Collider other in allCols)
        {
            if (other == null) continue;
            if (other.CompareTag("Target")) continue;
            if (other.transform.IsChildOf(transform)) continue;

            foreach (Collider mine in myCols)
                if (mine != null) Physics.IgnoreCollision(mine, other, false);
        }
    }

    // 한 프레임 후 카메라 등록 (초기 위치 안정화)
    System.Collections.IEnumerator RegisterCameraNextFrame()
    {
        yield return new WaitForSeconds(0.05f);
        MissileCamera cam = Camera.main?.GetComponent<MissileCamera>();
        if (cam != null) cam.SetTarget(transform, _rb);
    }

    void ExplodeNoTarget() => Explode(null, transform.position);

    // 콜라이더 충돌 감지
    void OnCollisionEnter(Collision col)
    {
        CancelInvoke(nameof(ExplodeNoTarget));
        Explode(col.gameObject);
    }

    // 트리거 충돌 감지 (H70 프리팹이 Trigger인 경우 대비)
    void OnTriggerEnter(Collider other)
    {
        CancelInvoke(nameof(ExplodeNoTarget));
        Explode(other.gameObject);
    }
}
