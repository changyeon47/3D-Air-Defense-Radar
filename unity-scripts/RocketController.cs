/*
 * RocketController.cs
 * IMU pitch/roll 값으로 로켓 오브젝트 방향을 제어하고,
 * fire == true일 때 미사일 프리팹을 발사한다.
 *
 * 사용법:
 *   - 씬의 로켓 GameObject에 이 스크립트를 추가
 *   - Inspector에서 missilePrefab에 미사일 프리팹 연결
 *   - missileLaunchPoint에 발사 위치 Transform 연결
 */

using UnityEngine;

public class RocketController : MonoBehaviour
{
    // ────────────────────────────────────────────
    // Inspector 설정
    // ────────────────────────────────────────────

    [Header("미사일 설정")]
    [Tooltip("발사할 미사일 프리팹")]
    public GameObject missilePrefab;

    [Tooltip("미사일이 생성될 발사 위치 (로켓 앞쪽 Transform)")]
    public Transform missileLaunchPoint;

    [Tooltip("미사일 비행 속도 (m/s)")]
    public float missileSpeed = 20f;

    [Tooltip("발사 후 쿨타임 (초)")]
    public float fireCooldown = 2f;

    [Header("회전 설정")]
    [Tooltip("IMU → 회전 민감도 배율")]
    public float rotationSensitivity = 1.0f;

    [Tooltip("회전 보간 속도 (높을수록 빠르게 반응)")]
    public float rotationSmoothing = 8f;

    [Tooltip("pitch/roll 최대 각도 제한 (도)")]
    public float maxAngle = 60f;

    [Header("이펙트")]
    [Tooltip("발사 시 재생할 파티클 이펙트 (선택)")]
    public ParticleSystem launchEffect;

    [Tooltip("발사 시 재생할 오디오 클립 (선택)")]
    public AudioClip launchSound;

    // ────────────────────────────────────────────
    // 내부 변수
    // ────────────────────────────────────────────
    private float       _lastFireTime  = -999f;   // 마지막 발사 시각
    private Quaternion  _targetRotation;           // 목표 회전값
    private AudioSource _audioSource;
    private bool        _prevFire = false;         // 이전 프레임 fire 상태 (엣지 감지)

    // ────────────────────────────────────────────
    // Unity 라이프사이클
    // ────────────────────────────────────────────
    void Awake()
    {
        _targetRotation = transform.rotation;

        // 오디오 소스 자동 추가
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        // 목표 회전으로 부드럽게 보간
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            _targetRotation,
            Time.deltaTime * rotationSmoothing
        );
    }

    // ────────────────────────────────────────────
    // 외부에서 센서 데이터 수신 (WebSocketReceiver가 호출)
    // ────────────────────────────────────────────
    public void OnSensorData(SensorData data)
    {
        UpdateRotation(data.pitch, data.roll);

        // fire: false → true 로 바뀌는 순간만 발사 (엣지 감지)
        if (data.fire && !_prevFire)
            TryFire();

        _prevFire = data.fire;
    }

    // ────────────────────────────────────────────
    // 회전 업데이트
    // ────────────────────────────────────────────
    private void UpdateRotation(float pitch, float roll)
    {
        // 각도 제한 적용
        float clampedPitch = Mathf.Clamp(pitch * rotationSensitivity, -maxAngle, maxAngle);
        float clampedRoll  = Mathf.Clamp(roll  * rotationSensitivity, -maxAngle, maxAngle);

        // pitch → X축 회전 (앞뒤), roll → Z축 회전 (좌우)
        _targetRotation = Quaternion.Euler(-clampedPitch, 0f, -clampedRoll);
    }

    // ────────────────────────────────────────────
    // 미사일 발사 시도 (쿨타임 체크)
    // ────────────────────────────────────────────
    private void TryFire()
    {
        if (Time.time - _lastFireTime < fireCooldown)
        {
            float remaining = fireCooldown - (Time.time - _lastFireTime);
            Debug.Log($"[Rocket] 쿨타임 중... {remaining:F1}초 남음");
            return;
        }

        LaunchMissile();
    }

    // ────────────────────────────────────────────
    // 미사일 발사
    // ────────────────────────────────────────────
    private void LaunchMissile()
    {
        _lastFireTime = Time.time;

        // 발사 위치: missileLaunchPoint가 없으면 로켓 위치에서 발사
        Transform spawnPoint = missileLaunchPoint != null ? missileLaunchPoint : transform;

        if (missilePrefab != null)
        {
            // 미사일 생성
            GameObject missile = Instantiate(missilePrefab, spawnPoint.position, spawnPoint.rotation);

            // Rigidbody가 있으면 초기 속도 부여
            Rigidbody rb = missile.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = spawnPoint.forward * missileSpeed;

            // 10초 후 자동 삭제 (씬 메모리 관리)
            Destroy(missile, 10f);

            Debug.Log($"[Rocket] 미사일 발사! 방향: {spawnPoint.forward}");
        }
        else
        {
            Debug.LogWarning("[Rocket] missilePrefab이 설정되지 않았습니다!");
        }

        // 이펙트 재생
        launchEffect?.Play();

        if (launchSound != null)
            _audioSource.PlayOneShot(launchSound);
    }

    // ────────────────────────────────────────────
    // 테스트용: 스페이스바로 강제 발사 (에디터 전용)
    // ────────────────────────────────────────────
#if UNITY_EDITOR
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 20), "[테스트] 스페이스바: 강제 발사");
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            TryFire();
    }
#endif
}
