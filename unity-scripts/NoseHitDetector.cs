/*
 * NoseHitDetector.cs
 * 미사일 노즈콘에 붙는 트리거 감지기.
 * 노즈 부분이 타겟에 닿을 때만 폭발을 유발한다.
 */

using UnityEngine;

public class NoseHitDetector : MonoBehaviour
{
    public MissileController missile;

    void OnTriggerEnter(Collider other)
    {
        if (missile == null) return;

        if (other.CompareTag("Target"))
            missile.OnNoseHit(other.gameObject, transform.position);
    }
}
