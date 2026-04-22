/*
 * LiDARTargetManager.cs
 * 라이다에서 수신한 위치로 Target 오브젝트를 생성/이동/제거한다.
 *
 * 타겟은 빨간 빛나는 구체로 표시되며,
 * 라이다 데이터가 오지 않으면 자동으로 사라진다.
 */

using System.Collections.Generic;
using UnityEngine;

public class LiDARTargetManager : MonoBehaviour
{
    [Header("타겟 표시 설정")]
    [Tooltip("3D LiDAR 시뮬레이터 사용 시 체크. Y 좌표를 수신 데이터에서 직접 읽음.\n" +
             "체크 해제 시 아래 targetHeight(고정값)를 사용 (2D LiDAR 호환 모드).")]
    public bool  use3DHeight    = true;

    [Tooltip("use3DHeight=false일 때 사용할 고정 Y 높이 (2D LiDAR 호환용)")]
    public float targetHeight   = 20f;

    [Tooltip("LiDAR 좌표(m)에 곱할 배율. 3D 모드에서는 X/Y/Z 모두 적용됨.")]
    public float positionScale  = 1f;

    [Tooltip("타겟 크기")]
    public float targetSize     = 2.5f;

    [Tooltip("위치 보간 속도 (부드러운 이동)")]
    public float lerpSpeed      = 4f;

    [Tooltip("이 시간 동안 감지 안 되면 타겟 제거 (초)")]
    public float removeTimeout  = 1.5f;

    // 타겟 ID → 런타임 정보
    private class TargetInfo
    {
        public GameObject obj;
        public Vector3    goalPos;
        public float      lastSeen;
    }

    private readonly Dictionary<int, TargetInfo> _targets = new();
    private Material _targetMat;

    void Awake()
    {
        _targetMat = new Material(Shader.Find("Standard"));
        _targetMat.color = new Color(1f, 0.08f, 0.08f);
        _targetMat.EnableKeyword("_EMISSION");
        _targetMat.SetColor("_EmissionColor", new Color(2f, 0f, 0f));
    }

    // LiDARReceiver가 프레임마다 호출
    public void UpdateTargets(LiDARTarget[] incoming)
    {
        float now = Time.time;
        var seenIds = new HashSet<int>();

        foreach (var t in incoming)
        {
            seenIds.Add(t.id);
            float goalY = (use3DHeight && t.y != 0f)
                ? t.y * positionScale
                : targetHeight;
            Vector3 goal = new Vector3(t.x * positionScale, goalY, t.z * positionScale);

            if (_targets.TryGetValue(t.id, out TargetInfo info))
            {
                info.goalPos  = goal;
                info.lastSeen = now;
            }
            else
            {
                // 새 타겟 생성
                GameObject obj = CreateTargetObject(t.id, goal);
                _targets[t.id] = new TargetInfo
                {
                    obj      = obj,
                    goalPos  = goal,
                    lastSeen = now,
                };
            }
        }

        // 오래된 타겟 제거
        var toRemove = new List<int>();
        foreach (var kv in _targets)
        {
            if (now - kv.Value.lastSeen > removeTimeout)
                toRemove.Add(kv.Key);
        }
        foreach (int id in toRemove)
        {
            if (_targets[id].obj != null)
                Destroy(_targets[id].obj);
            _targets.Remove(id);
        }
    }

    void Update()
    {
        // 타겟을 목표 위치로 부드럽게 이동
        foreach (var kv in _targets)
        {
            if (kv.Value.obj == null) continue;
            kv.Value.obj.transform.position = Vector3.Lerp(
                kv.Value.obj.transform.position,
                kv.Value.goalPos,
                Time.deltaTime * lerpSpeed
            );
        }
    }

    GameObject CreateTargetObject(int id, Vector3 pos)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = $"LiDAR_Target_{id}";
        obj.tag  = "Target";
        obj.transform.position   = pos;
        obj.transform.localScale = Vector3.one * targetSize;

        // 머티리얼 적용
        obj.GetComponent<Renderer>().material = _targetMat;

        // 발광 라이트
        Light lt = obj.AddComponent<Light>();
        lt.type      = LightType.Point;
        lt.color     = new Color(1f, 0.1f, 0.1f);
        lt.intensity = 2f;
        lt.range     = 6f;

        // 콜라이더는 Trigger로 (미사일 충돌 감지용)
        SphereCollider col = obj.GetComponent<SphereCollider>();
        col.isTrigger = true;

        Debug.Log($"[LiDAR] 타겟 생성: ID={id}, 위치={pos}");
        return obj;
    }
}
