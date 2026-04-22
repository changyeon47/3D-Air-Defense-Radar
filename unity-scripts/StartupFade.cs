/*
 * StartupFade.cs
 * 씬 시작 시 검은 화면을 가리고 페이드인한다.
 * 빈 GameObject에 추가하면 자동 동작.
 */

using UnityEngine;

public class StartupFade : MonoBehaviour
{
    [Tooltip("페이드인 시간 (초)")]
    public float fadeDuration = 0.6f;

    private Texture2D _blackTex;
    private float     _alpha = 1f;
    private bool      _done  = false;

    void Awake()
    {
        _blackTex = new Texture2D(1, 1);
        _blackTex.SetPixel(0, 0, Color.black);
        _blackTex.Apply();
    }

    void Update()
    {
        if (_done) return;
        _alpha -= Time.deltaTime / fadeDuration;
        if (_alpha <= 0f)
        {
            _alpha = 0f;
            _done  = true;
            Destroy(gameObject);
        }
    }

    void OnGUI()
    {
        if (_done) return;
        GUI.color = new Color(0f, 0f, 0f, _alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _blackTex);
        GUI.color = Color.white;
    }

    void OnDestroy()
    {
        if (_blackTex != null) Destroy(_blackTex);
    }
}
