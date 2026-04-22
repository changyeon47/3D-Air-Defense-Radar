/*
 * TargetSpawner.cs
 * 랜덤 위치에 적 오브젝트를 생성한다.
 * 빈 GameObject에 추가하면 자동으로 적이 스폰된다.
 */

using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    public int   maxTargets   = 5;      // 최대 동시 적 수
    public float spawnInterval = 3f;    // 스폰 간격 (초)

    [Header("스폰 범위")]
    public float rangeX  = 15f;         // X 범위
    public float rangeZ  = 15f;         // Z 범위
    public float minHeight = 5f;        // 최소 높이
    public float maxHeight = 20f;       // 최대 높이

    [Header("이동 설정")]
    public float moveSpeed = 2f;        // 적 이동 속도

    private float _timer;

    void Update()
    {
        // LiDAR 타겟만 사용 — 자동 스폰 비활성화
    }

    void SpawnTarget()
    {
        // 랜덤 위치 계산
        Vector3 spawnPos = new Vector3(
            Random.Range(-rangeX, rangeX),
            Random.Range(minHeight, maxHeight),
            Random.Range(-rangeZ, rangeZ)
        );

        // 적 오브젝트 생성
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        target.name = "Target";
        target.tag  = "Target";
        target.transform.position   = spawnPos;
        target.transform.localScale = Vector3.one * 0.8f;

        // 빨간 발광 머티리얼
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.1f, 0.1f);
        mat.SetFloat("_Metallic",   0.3f);
        mat.SetFloat("_Glossiness", 0.6f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.8f, 0f, 0f));
        target.GetComponent<Renderer>().material = mat;

        // 콜라이더 확실히 추가 (CreatePrimitive에 기본 포함이지만 명시적으로 확인)
        SphereCollider col = target.GetComponent<SphereCollider>();
        if (col == null) col = target.AddComponent<SphereCollider>();
        col.isTrigger = false;

        // 이동 스크립트 추가
        TargetMover mover = target.AddComponent<TargetMover>();
        mover.speed = moveSpeed;

        // 포인트 라이트 (붉은 빛)
        GameObject lo = new GameObject("Light");
        lo.transform.SetParent(target.transform, false);
        Light l = lo.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(1f, 0.1f, 0.1f);
        l.intensity = 1.5f;
        l.range     = 4f;
    }
}
