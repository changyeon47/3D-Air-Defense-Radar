/*
 * ExplosionEffect.cs
 * 코드로 원형 텍스처를 생성해서 자연스러운 폭발 파티클을 만든다.
 */

using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    private static Texture2D _softCircleTex;
    private static Texture2D _sparkTex;

    public static void Spawn(Vector3 position)
    {
        GameObject root = new GameObject("Explosion");
        root.transform.position = position;
        ExplosionEffect fx = root.AddComponent<ExplosionEffect>();
        fx.Play();
        Destroy(root, 4f);
    }

    // ── 부드러운 원형 텍스처 생성 ────────────────────────
    static Texture2D GetSoftCircle()
    {
        if (_softCircleTex != null) return _softCircleTex;

        int size = 64;
        _softCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
            float alpha = Mathf.Clamp01(1f - dist);
            alpha = alpha * alpha; // 가장자리 부드럽게
            _softCircleTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        _softCircleTex.Apply();
        return _softCircleTex;
    }

    // ── 날카로운 스파크 텍스처 생성 ──────────────────────
    static Texture2D GetSparkTex()
    {
        if (_sparkTex != null) return _sparkTex;

        int size = 32;
        _sparkTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
            float alpha = Mathf.Clamp01(1f - dist * 1.5f);
            alpha = Mathf.Pow(alpha, 3f);
            _sparkTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        _sparkTex.Apply();
        return _sparkTex;
    }

    // ── 파티클 머티리얼 생성 헬퍼 ────────────────────────
    Material MakeMat(Color color, Texture2D tex, bool additive = true)
    {
        string shaderName = additive ? "Particles/Additive" : "Particles/Alpha Blended";
        Shader shader = Shader.Find(shaderName);
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.mainTexture = tex;
        mat.color = color;
        return mat;
    }

    void Play()
    {
        CreateFireball();
        CreateSparks();
        CreateSmoke();
        CreateShockwave();
        CreateFlashLight();
    }

    // ── 1. 화염구 ─────────────────────────────────────────
    void CreateFireball()
    {
        GameObject obj = new GameObject("Fireball");
        obj.transform.SetParent(transform, false);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration        = 0.2f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1f, 5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.2f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sc = AnimationCurve.EaseInOut(0f, 0.2f, 1f, 0f);
        sc.AddKey(new Keyframe(0.2f, 1f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sc);

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f,   1f,   0.9f), 0.00f),
                new GradientColorKey(new Color(1f,   0.6f, 0.1f), 0.25f),
                new GradientColorKey(new Color(0.9f, 0.2f, 0.0f), 0.60f),
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.00f),
                new GradientAlphaKey(0.9f, 0.40f),
                new GradientAlphaKey(0.0f, 1.00f),
            }
        );
        colorOverLife.color = g;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material   = MakeMat(Color.white, GetSoftCircle(), true);

        ps.Play();
    }

    // ── 2. 불꽃 파편 ─────────────────────────────────────
    void CreateSparks()
    {
        GameObject obj = new GameObject("Sparks");
        obj.transform.SetParent(transform, false);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration        = 0.1f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(5f, 18f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.9f, 0.4f),
            new Color(1f, 0.4f, 0.1f));

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.1f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f,   0.9f, 0.3f), 0.0f),
                new GradientColorKey(new Color(1f,   0.3f, 0.0f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1.0f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0.0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1.0f),
            }
        );
        colorOverLife.color = g;

        // 트레일로 빛나는 선 효과
        var trails = ps.trails;
        trails.enabled          = true;
        trails.lifetime         = 0.12f;
        trails.ratio            = 1f;
        trails.widthOverTrail   = new ParticleSystem.MinMaxCurve(0.04f);
        trails.inheritParticleColor = true;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode   = ParticleSystemRenderMode.Billboard;
        renderer.material     = MakeMat(Color.white, GetSparkTex(), true);
        renderer.trailMaterial = MakeMat(new Color(1f, 0.5f, 0.1f), GetSparkTex(), true);

        ps.Play();
    }

    // ── 3. 연기 ──────────────────────────────────────────
    void CreateSmoke()
    {
        GameObject obj = new GameObject("Smoke");
        obj.transform.SetParent(transform, false);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration        = 0.4f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize       = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f);
        main.gravityModifier = -0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.3f, 0.3f, 0.7f),
            new Color(0.1f, 0.1f, 0.1f, 0.5f));

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.8f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sc = new AnimationCurve();
        sc.AddKey(0f, 0.3f);
        sc.AddKey(0.3f, 1f);
        sc.AddKey(1f, 1.8f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sc);

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 0f),
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.7f, 0.0f),
                new GradientAlphaKey(0.3f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f),
            }
        );
        colorOverLife.color = g;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material   = MakeMat(Color.white, GetSoftCircle(), false);

        ps.Play();
    }

    // ── 4. 충격파 링 ─────────────────────────────────────
    void CreateShockwave()
    {
        GameObject obj = new GameObject("Shockwave");
        obj.transform.SetParent(transform, false);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration        = 0.05f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = new Color(1f, 0.7f, 0.3f, 0.5f);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sc = new AnimationCurve();
        sc.AddKey(0f, 0f);
        sc.AddKey(0.4f, 10f);
        sc.AddKey(1f, 14f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sc);

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.8f, 0.4f), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.6f, 0.0f),
                new GradientAlphaKey(0.0f, 1.0f),
            }
        );
        colorOverLife.color = g;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material   = MakeMat(Color.white, GetSoftCircle(), true);

        ps.Play();
    }

    // ── 5. 플래시 라이트 ─────────────────────────────────
    void CreateFlashLight()
    {
        GameObject lo = new GameObject("FlashLight");
        lo.transform.SetParent(transform, false);
        Light l   = lo.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(1f, 0.6f, 0.2f);
        l.intensity = 20f;
        l.range     = 25f;
        StartCoroutine(FadeLight(l, 0.6f));
    }

    System.Collections.IEnumerator FadeLight(Light l, float duration)
    {
        float elapsed = 0f;
        float start   = l.intensity;
        while (elapsed < duration)
        {
            elapsed     += Time.deltaTime;
            l.intensity  = Mathf.Lerp(start, 0f, elapsed / duration);
            yield return null;
        }
        l.intensity = 0f;
    }
}
