/*
 * SceneSetup.cs
 * 씬 전체를 사실적인 군용 방공 기지 환경으로 설정한다.
 */

using UnityEngine;

public class SceneSetup : MonoBehaviour
{
    void Awake()
    {
        SetupSkybox();
        SetupLighting();
        SetupTerrain();
        SetupEnvironment();
        SetupLauncherBase();
        SetupFog();
        SetupCamera();
        SetupAimCamera();
    }

    // ── 스카이박스: 낮 하늘 ──────────────────────────────
    void SetupSkybox()
    {
        Camera cam = Camera.main;

        Material skyMat = new Material(Shader.Find("Skybox/Procedural"));
        if (skyMat == null || skyMat.shader == null || !skyMat.shader.isSupported)
        {
            // 스카이박스 셰이더 없으면 단색 하늘색으로 대체
            if (cam != null)
            {
                cam.clearFlags       = CameraClearFlags.SolidColor;
                cam.backgroundColor  = new Color(0.38f, 0.60f, 0.85f);
            }
            return;
        }

        skyMat.SetFloat("_SunSize",            0.04f);
        skyMat.SetFloat("_SunSizeConvergence",  5f);
        skyMat.SetFloat("_AtmosphereThickness", 1.0f);
        skyMat.SetColor("_SkyTint",    new Color(0.5f, 0.6f, 0.8f));
        skyMat.SetColor("_GroundColor", new Color(0.25f, 0.22f, 0.18f));
        skyMat.SetFloat("_Exposure",   1.2f);

        RenderSettings.skybox = skyMat;
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        DynamicGI.UpdateEnvironment();
    }

    // ── 조명: 강한 태양광 ────────────────────────────────
    void SetupLighting()
    {
        Light sun = FindFirstObjectByType<Light>();
        if (sun != null)
        {
            sun.type      = LightType.Directional;
            sun.color     = new Color(1.0f, 0.95f, 0.85f);
            sun.intensity = 1.4f;
            sun.transform.eulerAngles  = new Vector3(45f, -60f, 0f);
            sun.shadows                = LightShadows.Soft;
            sun.shadowStrength         = 0.7f;
        }

        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.0f;
    }

    // ── 지형: 사막 평원 + 언덕 ──────────────────────────
    void SetupTerrain()
    {
        // 메인 바닥
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position   = new Vector3(0f, -1.5f, 0f);
        ground.transform.localScale = new Vector3(50f, 1f, 50f);

        Material groundMat = new Material(Shader.Find("Standard"));
        groundMat.color = new Color(0.55f, 0.48f, 0.36f);   // 모래색
        groundMat.SetFloat("_Metallic",   0f);
        groundMat.SetFloat("_Glossiness", 0.05f);
        ground.GetComponent<Renderer>().material = groundMat;

        // 원거리 언덕들
        SpawnHill(new Vector3( 60f, -1.5f,  80f), new Vector3(40f, 8f, 30f), new Color(0.45f, 0.40f, 0.30f));
        SpawnHill(new Vector3(-80f, -1.5f,  60f), new Vector3(50f, 6f, 35f), new Color(0.48f, 0.42f, 0.32f));
        SpawnHill(new Vector3( 30f, -1.5f, -90f), new Vector3(45f, 10f, 40f), new Color(0.42f, 0.38f, 0.28f));
        SpawnHill(new Vector3(-50f, -1.5f, -70f), new Vector3(35f, 7f, 25f), new Color(0.50f, 0.44f, 0.33f));
        SpawnHill(new Vector3(100f, -1.5f,  10f), new Vector3(55f, 12f, 45f), new Color(0.44f, 0.39f, 0.29f));

        // 지면 돌/바위
        SpawnRock(new Vector3( 8f, -1.5f,  6f), 1.2f);
        SpawnRock(new Vector3(-6f, -1.5f,  9f), 0.8f);
        SpawnRock(new Vector3(12f, -1.5f, -5f), 1.5f);
        SpawnRock(new Vector3(-9f, -1.5f, -8f), 1.0f);
        SpawnRock(new Vector3( 4f, -1.5f, 14f), 0.6f);
        SpawnRock(new Vector3(-14f,-1.5f,  3f), 1.8f);
    }

    void SpawnHill(Vector3 pos, Vector3 scale, Color color)
    {
        GameObject hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hill.name = "Hill";
        hill.transform.position   = pos;
        hill.transform.localScale = scale;
        Destroy(hill.GetComponent<Collider>());

        Material m = new Material(Shader.Find("Standard"));
        m.color = color;
        m.SetFloat("_Metallic",   0f);
        m.SetFloat("_Glossiness", 0.02f);
        hill.GetComponent<Renderer>().material = m;
    }

    void SpawnRock(Vector3 pos, float size)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rock.name = "Rock";
        rock.transform.position   = pos;
        rock.transform.localScale = new Vector3(size, size * 0.6f, size * 0.8f);
        rock.transform.eulerAngles = new Vector3(
            Random.Range(-15f, 15f), Random.Range(0f, 90f), Random.Range(-10f, 10f));
        Destroy(rock.GetComponent<Collider>());

        Material m = new Material(Shader.Find("Standard"));
        m.color = new Color(0.35f, 0.32f, 0.28f);
        m.SetFloat("_Metallic",   0.1f);
        m.SetFloat("_Glossiness", 0.1f);
        rock.GetComponent<Renderer>().material = m;
    }

    // ── 주변 환경: 군용 시설물 ───────────────────────────
    void SetupEnvironment()
    {
        Material metalMat = new Material(Shader.Find("Standard"));
        metalMat.color = new Color(0.3f, 0.32f, 0.28f);
        metalMat.SetFloat("_Metallic",   0.7f);
        metalMat.SetFloat("_Glossiness", 0.4f);

        Material concreteMat = new Material(Shader.Find("Standard"));
        concreteMat.color = new Color(0.55f, 0.54f, 0.52f);
        concreteMat.SetFloat("_Metallic",   0.0f);
        concreteMat.SetFloat("_Glossiness", 0.1f);

        // 레이더 타워 (Military Radar 에셋 없을 때 대체용)
        SpawnRadarTower(new Vector3(12f, -1.5f, 8f), metalMat, concreteMat);

        // 벙커/방어 시설
        SpawnBunker(new Vector3(-10f, -1.5f, 12f), concreteMat);
        SpawnBunker(new Vector3(-14f, -1.5f, -8f), concreteMat);

        // 철조망 펜스
        SpawnFenceLine(new Vector3(-20f, -1.5f, 0f), 40f, true,  metalMat);
        SpawnFenceLine(new Vector3(  0f, -1.5f, 20f), 40f, false, metalMat);
        SpawnFenceLine(new Vector3(  0f, -1.5f,-20f), 40f, false, metalMat);
        SpawnFenceLine(new Vector3( 20f, -1.5f, 0f), 40f, true,  metalMat);

        // 군용 표시등 (포스트)
        SpawnLightPost(new Vector3( 5f, -1.5f,  5f));
        SpawnLightPost(new Vector3(-5f, -1.5f,  5f));
        SpawnLightPost(new Vector3( 5f, -1.5f, -5f));
        SpawnLightPost(new Vector3(-5f, -1.5f, -5f));
    }

    void SpawnRadarTower(Vector3 pos, Material metalMat, Material concreteMat)
    {
        // 타워 기둥
        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tower.name = "RadarTower";
        tower.transform.position   = pos + new Vector3(0f, 4f, 0f);
        tower.transform.localScale = new Vector3(0.4f, 4f, 0.4f);
        tower.GetComponent<Renderer>().material = metalMat;

        // 타워 베이스
        GameObject towerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        towerBase.transform.position   = pos + new Vector3(0f, 0.3f, 0f);
        towerBase.transform.localScale = new Vector3(1.5f, 0.3f, 1.5f);
        towerBase.GetComponent<Renderer>().material = concreteMat;

        // 레이더 접시 (Sphere 찌그러뜨림)
        GameObject dish = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dish.name = "RadarDish";
        dish.transform.position   = pos + new Vector3(0f, 8.5f, 0f);
        dish.transform.localScale = new Vector3(2.5f, 0.3f, 2.5f);
        dish.transform.eulerAngles = new Vector3(30f, 0f, 0f);
        dish.GetComponent<Renderer>().material = metalMat;
        Destroy(dish.GetComponent<Collider>());

        // 레이더 회전 연출
        dish.AddComponent<RadarRotator>();
    }

    void SpawnBunker(Vector3 pos, Material mat)
    {
        GameObject bunker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bunker.name = "Bunker";
        bunker.transform.position   = pos + new Vector3(0f, 0.5f, 0f);
        bunker.transform.localScale = new Vector3(4f, 1.5f, 3f);
        bunker.GetComponent<Renderer>().material = mat;

        // 지붕 경사
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.position   = pos + new Vector3(0f, 1.6f, 0f);
        roof.transform.localScale = new Vector3(4.2f, 0.3f, 3.2f);
        roof.transform.eulerAngles = new Vector3(5f, 0f, 0f);
        roof.GetComponent<Renderer>().material = mat;
        Destroy(roof.GetComponent<Collider>());
    }

    void SpawnFenceLine(Vector3 start, float length, bool alongX, Material mat)
    {
        int posts = (int)(length / 3f);
        for (int i = 0; i < posts; i++)
        {
            Vector3 offset = alongX
                ? new Vector3(-length / 2f + i * 3f, 0f, 0f)
                : new Vector3(0f, 0f, -length / 2f + i * 3f);

            // 기둥
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "FencePost";
            post.transform.position   = start + offset + new Vector3(0f, 0.6f, 0f);
            post.transform.localScale = new Vector3(0.08f, 0.6f, 0.08f);
            post.GetComponent<Renderer>().material = mat;
            Destroy(post.GetComponent<Collider>());

            // 가로대
            if (i < posts - 1)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "FenceRail";
                Vector3 railOffset = alongX
                    ? new Vector3(1.5f, 0f, 0f) : new Vector3(0f, 0f, 1.5f);
                rail.transform.position   = start + offset + railOffset + new Vector3(0f, 1.0f, 0f);
                rail.transform.localScale = alongX
                    ? new Vector3(3f, 0.05f, 0.05f) : new Vector3(0.05f, 0.05f, 3f);
                rail.GetComponent<Renderer>().material = mat;
                Destroy(rail.GetComponent<Collider>());
            }
        }
    }

    void SpawnLightPost(Vector3 pos)
    {
        Material postMat = new Material(Shader.Find("Standard"));
        postMat.color = new Color(0.3f, 0.3f, 0.3f);
        postMat.SetFloat("_Metallic", 0.8f);

        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "LightPost";
        post.transform.position   = pos + new Vector3(0f, 2f, 0f);
        post.transform.localScale = new Vector3(0.06f, 2f, 0.06f);
        post.GetComponent<Renderer>().material = postMat;
        Destroy(post.GetComponent<Collider>());

        // 노란 포인트 라이트
        GameObject lo = new GameObject("PostLight");
        lo.transform.position = pos + new Vector3(0f, 4.2f, 0f);
        Light l = lo.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(1f, 0.9f, 0.5f);
        l.intensity = 1.2f;
        l.range     = 8f;
    }

    // ── 런처 발사대 콘크리트 기반 ────────────────────────
    void SetupLauncherBase()
    {
        Material concreteMat = new Material(Shader.Find("Standard"));
        concreteMat.color = new Color(0.50f, 0.50f, 0.48f);
        concreteMat.SetFloat("_Metallic",   0.05f);
        concreteMat.SetFloat("_Glossiness", 0.15f);

        Material metalMat = new Material(Shader.Find("Standard"));
        metalMat.color = new Color(0.28f, 0.30f, 0.26f);
        metalMat.SetFloat("_Metallic",   0.85f);
        metalMat.SetFloat("_Glossiness", 0.4f);

        // 콘크리트 패드
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "LaunchPad";
        pad.transform.position   = new Vector3(0f, -1.42f, 0f);
        pad.transform.localScale = new Vector3(8f, 0.18f, 8f);
        pad.GetComponent<Renderer>().material = concreteMat;

        // 패드 테두리 금속 프레임
        SpawnPadFrame(metalMat);

        // 발사대 지지대 4개
        SpawnSupportLeg(new Vector3( 1.5f, -1.5f,  1.5f), metalMat);
        SpawnSupportLeg(new Vector3(-1.5f, -1.5f,  1.5f), metalMat);
        SpawnSupportLeg(new Vector3( 1.5f, -1.5f, -1.5f), metalMat);
        SpawnSupportLeg(new Vector3(-1.5f, -1.5f, -1.5f), metalMat);

        // 유압 실린더 2개
        SpawnHydraulic(new Vector3( 0.8f, -1.5f, 0f), metalMat);
        SpawnHydraulic(new Vector3(-0.8f, -1.5f, 0f), metalMat);

        // 케이블 박스
        SpawnBox(new Vector3( 3f, -1.42f,  3f), new Vector3(1f, 0.6f, 0.8f), concreteMat);
        SpawnBox(new Vector3(-3f, -1.42f,  3f), new Vector3(0.8f, 0.5f, 0.6f), metalMat);
    }

    void SpawnPadFrame(Material mat)
    {
        // 4면 프레임
        float s = 4f;
        float t = 0.12f;
        SpawnBox(new Vector3( s,  -1.33f, 0f), new Vector3(t, t, s * 2f), mat);
        SpawnBox(new Vector3(-s,  -1.33f, 0f), new Vector3(t, t, s * 2f), mat);
        SpawnBox(new Vector3( 0f, -1.33f,  s), new Vector3(s * 2f, t, t), mat);
        SpawnBox(new Vector3( 0f, -1.33f, -s), new Vector3(s * 2f, t, t), mat);
    }

    void SpawnSupportLeg(Vector3 pos, Material mat)
    {
        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leg.name = "SupportLeg";
        leg.transform.position   = pos + new Vector3(0f, 0.5f, 0f);
        leg.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
        leg.GetComponent<Renderer>().material = mat;
        Destroy(leg.GetComponent<Collider>());
    }

    void SpawnHydraulic(Vector3 pos, Material mat)
    {
        GameObject hyd = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hyd.name = "Hydraulic";
        hyd.transform.position    = pos + new Vector3(0f, 0.8f, 0f);
        hyd.transform.localScale  = new Vector3(0.08f, 0.8f, 0.08f);
        hyd.transform.eulerAngles = new Vector3(30f, 0f, 0f);
        hyd.GetComponent<Renderer>().material = mat;
        Destroy(hyd.GetComponent<Collider>());
    }

    void SpawnBox(Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.position   = pos;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().material = mat;
        Destroy(box.GetComponent<Collider>());
    }

    // ── 안개 ─────────────────────────────────────────────
    void SetupFog()
    {
        RenderSettings.fog             = true;
        RenderSettings.fogColor        = new Color(0.65f, 0.68f, 0.72f);
        RenderSettings.fogMode         = FogMode.Linear;
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance   = 200f;
    }

    // ── 카메라 ────────────────────────────────────────────
    void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        cam.farClipPlane = 500f;
        // 위치/회전은 MissileCamera가 담당하므로 여기서는 건드리지 않음
    }

    void SetupAimCamera()
    {
        new GameObject("AimCameraController").AddComponent<AimCamera>();
    }
}

// ── 레이더 회전 컴포넌트 ─────────────────────────────────
public class RadarRotator : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
    }
}
