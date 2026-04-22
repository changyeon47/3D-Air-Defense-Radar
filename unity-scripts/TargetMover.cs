/*
 * TargetMover.cs
 * 적 오브젝트가 랜덤 방향으로 천천히 이동한다.
 * TargetSpawner가 자동으로 추가하는 스크립트.
 */

using UnityEngine;

public class TargetMover : MonoBehaviour
{
    public float speed = 2f;

    private Vector3 _direction;
    private float   _changeTimer;
    private float   _changeInterval;

    void Start()
    {
        PickNewDirection();
    }

    void Update()
    {
        // 이동
        transform.position += _direction * speed * Time.deltaTime;

        // 일정 시간마다 방향 변경
        _changeTimer += Time.deltaTime;
        if (_changeTimer >= _changeInterval)
            PickNewDirection();

        // 너무 높이 올라가거나 내려가면 방향 반전
        if (transform.position.y > 25f || transform.position.y < 3f)
            _direction.y = -_direction.y;

        // 범위 벗어나면 방향 반전
        if (Mathf.Abs(transform.position.x) > 20f) _direction.x = -_direction.x;
        if (Mathf.Abs(transform.position.z) > 20f) _direction.z = -_direction.z;
    }

    void PickNewDirection()
    {
        // 랜덤 방향 (주로 수평 이동, 약간의 수직)
        _direction = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-0.3f, 0.3f),
            Random.Range(-1f, 1f)
        ).normalized;

        _changeTimer    = 0f;
        _changeInterval = Random.Range(2f, 5f);
    }
}
