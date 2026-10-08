using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Membangun scene Praktikum 06 secara otomatis (Ground, Wall di layer Obstacle, NavMesh bake,
// Player, Enemy, waypoint, SafePoint, dan seluruh reference Inspector).
//
//   P06_BehaviorTree : Enemy Behavior Tree utama (Patrol/Chase/Attack/Flee + Search, Health Bar,
//                      Animator, On-screen BT Debugger)
//   P06_Personality  : AggressiveEnemy vs CowardEnemy (Behavior Tree sama, parameter berbeda)
//   P06_UtilityAI    : Enemy dengan Full Utility AI Controller
//
// Menu: Praktikum 06 > ...
public static class Praktikum06SceneBuilder
{
    private enum SceneKind { Main, Personality, Utility }

    private const string ObstacleLayerName = "Obstacle";
    private const string PlayerLayerName = "Player";
    private const string AnimFolder = "Assets/Animations";

    private static string ScenePath(string sceneName) { return "Assets/Scenes/" + sceneName + ".unity"; }

    [MenuItem("Praktikum 06/Build Behavior Tree Scene")]
    public static void BuildMain()
    {
        if (!ConfirmSave()) return;
        Build("P06_BehaviorTree", SceneKind.Main);
    }

    [MenuItem("Praktikum 06/Build Personality Scene")]
    public static void BuildPersonality()
    {
        if (!ConfirmSave()) return;
        Build("P06_Personality", SceneKind.Personality);
    }

    [MenuItem("Praktikum 06/Build Utility AI Scene")]
    public static void BuildUtility()
    {
        if (!ConfirmSave()) return;
        Build("P06_UtilityAI", SceneKind.Utility);
    }

    [MenuItem("Praktikum 06/Build All Scenes")]
    public static void BuildAll()
    {
        if (!ConfirmSave()) return;
        Build("P06_Personality", SceneKind.Personality);
        Build("P06_UtilityAI", SceneKind.Utility);
        Build("P06_BehaviorTree", SceneKind.Main); // terakhir agar scene utama yang terbuka
    }

    private static bool ConfirmSave()
    {
        return Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    // ------------------------------------------------------------------

    private static void Build(string sceneName, SceneKind kind)
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Animations");

        int obstacleLayer = EnsureLayer(ObstacleLayerName);
        int playerLayer = EnsureLayer(PlayerLayerName);

        Material groundMat = GetMaterial("GroundMaterial", new Color(0.17f, 0.20f, 0.26f));
        Material obstacleMat = GetMaterial("ObstacleMaterial", new Color(0.42f, 0.46f, 0.56f));
        Material playerMat = GetMaterial("PlayerMaterial", new Color(0.25f, 0.55f, 1f));
        Material noseMat = GetMaterial("MarkerMaterial", Color.white);
        Material waypointMat = GetMaterial("WaypointMaterial", new Color(1f, 0.78f, 0.25f));
        Material safeMat = GetMaterial("SafeZoneMaterial", new Color(0.2f, 0.9f, 0.7f));

        AnimatorController animController = BuildEnemyAnimator();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ---------- Kamera (top-down miring) ----------
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 22f, -17f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
        }

        // ---------- Environment ----------
        GameObject environment = new GameObject("Environment");

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(environment.transform);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        CreateWall("Wall01", environment.transform, new Vector3(0f, 1f, 3f), new Vector3(6f, 2f, 1f), obstacleLayer, obstacleMat);
        CreateWall("Wall02", environment.transform, new Vector3(-4f, 1f, -2f), new Vector3(1f, 2f, 5f), obstacleLayer, obstacleMat);
        CreateWall("Wall03", environment.transform, new Vector3(5f, 1f, -3f), new Vector3(4f, 2f, 1f), obstacleLayer, obstacleMat);

        // ---------- Navigation ----------
        GameObject navigation = new GameObject("Navigation");
        NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();

        // ---------- Patrol Points & SafePoint ----------
        GameObject patrolRoot = new GameObject("PatrolPoints");
        Transform w1 = CreateEmpty("Waypoint01", patrolRoot.transform, new Vector3(-7f, 0f, -7f));
        Transform w2 = CreateEmpty("Waypoint02", patrolRoot.transform, new Vector3(-7f, 0f, 7f));
        Transform w3 = CreateEmpty("Waypoint03", patrolRoot.transform, new Vector3(7f, 0f, 7f));
        Transform w4 = CreateEmpty("Waypoint04", patrolRoot.transform, new Vector3(7f, 0f, -7f));
        foreach (Transform w in new[] { w1, w2, w3, w4 })
            CreateDisc(w, 0.55f, waypointMat);

        Object[] patrolForward = { w1, w2, w3, w4 };
        Object[] patrolReverse = { w4, w3, w2, w1 };

        Transform safePoint = CreateEmpty("SafePoint", null, new Vector3(-10f, 0f, -10f));
        CreateDisc(safePoint, 1.3f, safeMat);

        // ---------- Player ----------
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.layer = playerLayer;
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.GetComponent<Renderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>()); // digantikan CharacterController
        player.AddComponent<CharacterController>();
        PlayerHealth playerHealth = player.AddComponent<PlayerHealth>();
        player.AddComponent<SimplePlayerController>();
        IgnoreInNavMeshBuild(player);
        CreateMarker(player.transform, noseMat);

        // Health Player ditampilkan di PlayerHUD (kartu bawah layar), bukan nameplate di atas kepala,
        // agar tidak menimpa nameplate Enemy saat berdekatan.
        GameObject playerHudObject = new GameObject("PlayerHUD");
        SetRef(playerHudObject.AddComponent<PlayerHUD>(), "playerHealth", playerHealth);

        int mask = 1 << obstacleLayer;

        // ---------- Enemy + HUD sesuai jenis scene ----------
        GameObject selected;

        if (kind == SceneKind.Main)
        {
            Material enemyMat = GetMaterial("EnemyMaterial", new Color(0.2f, 0.8f, 0.3f));
            GameObject enemy = CreateEnemyBody("Enemy", new Vector3(6f, 1f, 5f), enemyMat, noseMat, animController, out Renderer modelRenderer);

            EnemyBTController bt = SetupBehaviorTreeBrain(enemy, player.transform, safePoint, patrolForward, mask, "ENEMY", new Color(0.3f, 0.85f, 0.45f));

            GameObject hud = new GameObject("BTDebugHUD");
            EnemyBTDebugHUD debugHud = hud.AddComponent<EnemyBTDebugHUD>();
            SetRef(debugHud, "enemy", bt);
            SetRef(debugHud, "playerHealth", playerHealth);
            SetRef(debugHud, "bodyRenderer", modelRenderer);

            selected = enemy;
        }
        else if (kind == SceneKind.Personality)
        {
            Transform safePoint2 = CreateEmpty("SafePoint02", null, new Vector3(10f, 0f, -11f));
            CreateDisc(safePoint2, 1.3f, safeMat);

            Material aggressiveMat = GetMaterial("AggressiveMaterial", new Color(0.9f, 0.15f, 0.15f));
            Material cowardMat = GetMaterial("CowardMaterial", new Color(0.95f, 0.85f, 0.3f));

            // Aggressive: mudah melihat, cepat mengejar, sering menyerang, jarang kabur.
            GameObject aggressive = CreateEnemyBody("AggressiveEnemy", new Vector3(8f, 1f, 6f), aggressiveMat, noseMat, animController, out _);
            EnemyBTController aggressiveBt = SetupBehaviorTreeBrain(aggressive, player.transform, safePoint, patrolForward, mask, "AGGRESSIVE", new Color(0.95f, 0.28f, 0.30f));
            SetFloat(aggressiveBt, "visionRange", 15f);
            SetFloat(aggressiveBt, "visionAngle", 150f);
            SetFloat(aggressiveBt, "chaseSpeed", 5f);
            SetFloat(aggressiveBt, "attackRange", 2.5f);
            SetFloat(aggressiveBt, "attackCooldown", 0.8f);
            SetInt(aggressiveBt, "lowHealthThreshold", 15);

            // Coward: penglihatan biasa, lambat mengejar, jarang menyerang, cepat kabur.
            GameObject coward = CreateEnemyBody("CowardEnemy", new Vector3(-8f, 1f, 7f), cowardMat, noseMat, animController, out _);
            EnemyBTController cowardBt = SetupBehaviorTreeBrain(coward, player.transform, safePoint2, patrolReverse, mask, "COWARD", new Color(1f, 0.88f, 0.3f));
            SetFloat(cowardBt, "visionRange", 10f);
            SetFloat(cowardBt, "visionAngle", 90f);
            SetFloat(cowardBt, "chaseSpeed", 3f);
            SetFloat(cowardBt, "attackCooldown", 2f);
            SetFloat(cowardBt, "fleeSpeed", 6f);
            SetInt(cowardBt, "lowHealthThreshold", 70);

            GameObject hud = new GameObject("PersonalityHUD");
            EnemyListHUD listHud = hud.AddComponent<EnemyListHUD>();
            SetString(listHud, "title", "PERSONALITY ENEMY");
            SetRefArray(listHud, "enemies", new Object[] { aggressiveBt, cowardBt });
            SetStringArray(listHud, "descriptions", new[]
            {
                "Vision 15 / 150deg  Chase 5  Cooldown 0.8  Flee HP<=15",
                "Vision 10 / 90deg  Chase 3  Cooldown 2.0  Flee HP<=70"
            });
            SetRef(listHud, "playerHealth", playerHealth);

            selected = aggressive;
        }
        else
        {
            Material utilityMat = GetMaterial("UtilityMaterial", new Color(0.7f, 0.3f, 0.9f));
            GameObject enemy = CreateEnemyBody("UtilityEnemy", new Vector3(6f, 1f, 5f), utilityMat, noseMat, animController, out _);

            EnemyPerception perception = enemy.AddComponent<EnemyPerception>();
            SetRef(perception, "player", player.transform);
            SetLayerMask(perception, "obstacleMask", mask);

            EnemyUtilityController utility = enemy.AddComponent<EnemyUtilityController>();
            SetRef(utility, "safePoint", safePoint);
            SetRefArray(utility, "patrolPoints", patrolForward);

            AddSupportComponents(enemy, "UTILITY AI", new Color(0.72f, 0.45f, 1f));

            GameObject hud = new GameObject("UtilityHUD");
            EnemyListHUD listHud = hud.AddComponent<EnemyListHUD>();
            SetString(listHud, "title", "UTILITY AI ENEMY");
            SetRefArray(listHud, "enemies", new Object[] { utility });
            SetStringArray(listHud, "descriptions", new[]
            {
                "Skor tertinggi menang + Commitment 1s + Hysteresis 0.15"
            });
            SetRef(listHud, "playerHealth", playerHealth);

            selected = enemy;
        }

        // ---------- Bake NavMesh ----------
        surface.BuildNavMesh();

        string navFolder = "Assets/Scenes/" + sceneName;

        if (surface.navMeshData == null)
        {
            Debug.LogError("[Praktikum06] NavMesh gagal di-bake. Bake manual lewat NavMeshSurface > Bake.");
        }
        else
        {
            EnsureFolder("Assets/Scenes", sceneName);
            string navPath = navFolder + "/NavMesh-Navigation.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            EditorUtility.SetDirty(surface);
        }

        EditorSceneManager.SaveScene(scene, ScenePath(sceneName));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EnsureSceneInBuildSettings(ScenePath(sceneName));

        Selection.activeGameObject = selected;

        Debug.Log("[Praktikum06] Scene dibuat: " + ScenePath(sceneName) + ". Tekan Play untuk menguji.");
    }

    // Bake ulang NavMesh pada scene yang sedang terbuka (mis. setelah memindahkan wall).
    [MenuItem("Praktikum 06/Rebake NavMesh")]
    public static void Rebake()
    {
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();

        if (surface == null)
        {
            Debug.LogError("[Praktikum06] NavMeshSurface tidak ditemukan di scene ini.");
            return;
        }

        Scene scene = surface.gameObject.scene;
        string navFolder = "Assets/Scenes/" + scene.name;

        string oldPath = surface.navMeshData != null
            ? AssetDatabase.GetAssetPath(surface.navMeshData)
            : "";

        surface.BuildNavMesh();

        if (surface.navMeshData == null)
        {
            Debug.LogError("[Praktikum06] Bake gagal.");
            return;
        }

        string newPath = string.IsNullOrEmpty(oldPath)
            ? navFolder + "/NavMesh-Navigation.asset"
            : oldPath;

        if (string.IsNullOrEmpty(oldPath))
            EnsureFolder("Assets/Scenes", scene.name);
        else
            AssetDatabase.DeleteAsset(oldPath);

        AssetDatabase.CreateAsset(surface.navMeshData, newPath);
        EditorUtility.SetDirty(surface);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Praktikum06] NavMesh di-bake ulang dan scene disimpan.");
    }

    // Tombol Restart (R) memuat ulang scene lewat nama, sehingga scene harus ada di Build Settings.
    private static void EnsureSceneInBuildSettings(string path)
    {
        if (!System.IO.File.Exists(path))
            return;

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        foreach (EditorBuildSettingsScene existing in scenes)
        {
            if (existing.path == path)
                return;
        }

        EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[scenes.Length + 1];
        System.Array.Copy(scenes, updated, scenes.Length);
        updated[scenes.Length] = new EditorBuildSettingsScene(path, true);
        EditorBuildSettings.scenes = updated;
    }

    // ------------------------------------------------------------------
    // Enemy
    // ------------------------------------------------------------------

    // Root (NavMeshAgent + collider) dengan child "Model" yang dianimasikan oleh Animator.
    private static GameObject CreateEnemyBody(
        string name, Vector3 position, Material mat, Material noseMat,
        AnimatorController controller, out Renderer modelRenderer)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.5f;

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.speed = 3f;
        agent.angularSpeed = 360f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0f;
        agent.baseOffset = 1f; // pivot kapsul di tengah, NavMesh di lantai
        IgnoreInNavMeshBuild(root);

        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        model.name = "Model";
        Object.DestroyImmediate(model.GetComponent<CapsuleCollider>());
        model.transform.SetParent(root.transform, false);
        modelRenderer = model.GetComponent<Renderer>();
        modelRenderer.sharedMaterial = mat;
        CreateMarker(model.transform, noseMat);

        Animator animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        return root;
    }

    private static EnemyBTController SetupBehaviorTreeBrain(
        GameObject enemy, Transform player, Transform safePoint, Object[] patrol, int obstacleMask,
        string displayName, Color accent)
    {
        EnemyBTController bt = enemy.AddComponent<EnemyBTController>();
        SetRef(bt, "player", player);
        SetRef(bt, "safePoint", safePoint);
        SetRefArray(bt, "patrolPoints", patrol);
        SetLayerMask(bt, "obstacleMask", obstacleMask);

        AddSupportComponents(enemy, displayName, accent);
        return bt;
    }

    // Nameplate + health bar dan Animator Driver (membaca IEnemyBrain pada Enemy yang sama).
    private static void AddSupportComponents(GameObject enemy, string displayName, Color accent)
    {
        HealthNameplate plate = enemy.AddComponent<HealthNameplate>();
        SetString(plate, "displayName", displayName);
        SetColor(plate, "accent", accent);

        EnemyAnimatorDriver driver = enemy.AddComponent<EnemyAnimatorDriver>();
        SetRef(driver, "animator", enemy.GetComponentInChildren<Animator>());
    }

    // ------------------------------------------------------------------
    // Animator (clip dibuat lewat kode: bobbing, lean, lunge pada child "Model")
    // ------------------------------------------------------------------

    private static AnimatorController BuildEnemyAnimator()
    {
        AnimationClip idle = MakeClip("Enemy_Idle", true,
            ("localScale.y", Curve(0f, 1f, 1f, 1.04f, 2f, 1f)));

        AnimationClip walk = MakeClip("Enemy_Walk", true,
            ("localPosition.y", Curve(0f, 0f, 0.25f, 0.08f, 0.5f, 0f, 0.75f, 0.08f, 1f, 0f)),
            ("localEulerAnglesRaw.z", Curve(0f, 0f, 0.25f, 4f, 0.5f, 0f, 0.75f, -4f, 1f, 0f)));

        AnimationClip run = MakeClip("Enemy_Run", true,
            ("localPosition.y", Curve(0f, 0f, 0.125f, 0.15f, 0.25f, 0f, 0.375f, 0.15f, 0.5f, 0f)),
            ("localEulerAnglesRaw.x", Curve(0f, 15f, 0.5f, 15f)),
            ("localEulerAnglesRaw.z", Curve(0f, 0f, 0.125f, 6f, 0.25f, 0f, 0.375f, -6f, 0.5f, 0f)));

        AnimationClip attack = MakeClip("Enemy_Attack", false,
            ("localPosition.z", Curve(0f, 0f, 0.12f, 0.7f, 0.5f, 0f)),
            ("localEulerAnglesRaw.x", Curve(0f, 0f, 0.12f, 35f, 0.5f, 0f)),
            ("localScale.y", Curve(0f, 1f, 0.12f, 0.9f, 0.5f, 1f)));

        AnimationClip flee = MakeClip("Enemy_Flee", true,
            ("localPosition.y", Curve(0f, 0f, 0.1f, 0.12f, 0.2f, 0f, 0.3f, 0.12f, 0.4f, 0f)),
            ("localEulerAnglesRaw.x", Curve(0f, -12f, 0.4f, -12f)),
            ("localEulerAnglesRaw.z", Curve(0f, 0f, 0.1f, 10f, 0.2f, 0f, 0.3f, -10f, 0.4f, 0f)),
            ("localScale.y", Curve(0f, 0.92f, 0.4f, 0.92f)));

        string path = AnimFolder + "/EnemyAnimator.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsFleeing", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState sIdle = sm.AddState("Idle", new Vector3(250, 0, 0));
        AnimatorState sWalk = sm.AddState("Walk", new Vector3(500, -80, 0));
        AnimatorState sRun = sm.AddState("Run", new Vector3(750, -80, 0));
        AnimatorState sAttack = sm.AddState("Attack", new Vector3(250, 160, 0));
        AnimatorState sFlee = sm.AddState("Flee", new Vector3(500, 160, 0));

        sIdle.motion = idle;
        sWalk.motion = walk;
        sRun.motion = run;
        sAttack.motion = attack;
        sFlee.motion = flee;
        sm.defaultState = sIdle;

        // Locomotion (berdasarkan Speed NavMeshAgent)
        Transition(sIdle, sWalk, 0.15f).AddCondition(AnimatorConditionMode.Greater, 0.3f, "Speed");
        Transition(sWalk, sIdle, 0.15f).AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");
        Transition(sWalk, sRun, 0.15f).AddCondition(AnimatorConditionMode.Greater, 3.2f, "Speed");
        Transition(sRun, sWalk, 0.15f).AddCondition(AnimatorConditionMode.Less, 2.8f, "Speed");

        // Attack (trigger dari state mana pun), lalu kembali ke Idle
        AnimatorStateTransition anyAttack = sm.AddAnyStateTransition(sAttack);
        anyAttack.hasExitTime = false;
        anyAttack.duration = 0.05f;
        anyAttack.canTransitionToSelf = false;
        anyAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

        AnimatorStateTransition attackToIdle = sAttack.AddTransition(sIdle);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.9f;
        attackToIdle.duration = 0.1f;

        // Flee (bool dari state mana pun)
        AnimatorStateTransition anyFlee = sm.AddAnyStateTransition(sFlee);
        anyFlee.hasExitTime = false;
        anyFlee.duration = 0.1f;
        anyFlee.canTransitionToSelf = false;
        anyFlee.AddCondition(AnimatorConditionMode.If, 0f, "IsFleeing");

        Transition(sFlee, sIdle, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsFleeing");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        return t;
    }

    // Curve dari pasangan (waktu, nilai, waktu, nilai, ...).
    private static AnimationCurve Curve(params float[] timeValuePairs)
    {
        AnimationCurve curve = new AnimationCurve();

        for (int i = 0; i + 1 < timeValuePairs.Length; i += 2)
            curve.AddKey(timeValuePairs[i], timeValuePairs[i + 1]);

        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);

        return curve;
    }

    private static AnimationClip MakeClip(string clipName, bool loop, params (string property, AnimationCurve curve)[] curves)
    {
        AnimationClip clip = new AnimationClip { name = clipName };

        foreach ((string property, AnimationCurve curve) in curves)
            clip.SetCurve("", typeof(Transform), property, curve);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path = AnimFolder + "/" + clipName + ".anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // ------------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------------

    private static void CreateWall(string name, Transform parent, Vector3 position, Vector3 scale, int layer, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.layer = layer;
        wall.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Transform CreateEmpty(string name, Transform parent, Vector3 position)
    {
        GameObject go = new GameObject(name);
        if (parent != null)
            go.transform.SetParent(parent);
        go.transform.position = position;
        return go.transform;
    }

    // Kubus kecil di depan kapsul agar arah hadap (forward) terlihat saat demo FOV.
    private static void CreateMarker(Transform parent, Material mat)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "FacingMarker";
        Object.DestroyImmediate(marker.GetComponent<BoxCollider>());
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
        marker.transform.localScale = new Vector3(0.3f, 0.3f, 0.5f);
        marker.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // Penanda lantai datar (waypoint / safe zone); tidak ikut collision maupun NavMesh.
    private static void CreateDisc(Transform parent, float radius, Material mat)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Marker";
        Object.DestroyImmediate(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(parent, false);
        disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        disc.GetComponent<Renderer>().sharedMaterial = mat;
        IgnoreInNavMeshBuild(parent.gameObject);
    }

    // Player/Enemy jangan ikut di-bake menjadi bagian NavMesh.
    private static void IgnoreInNavMeshBuild(GameObject go)
    {
        NavMeshModifier modifier = go.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
    }

    private static Material GetMaterial(string name, Color color)
    {
        string path = "Assets/Materials/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color; // selalu sinkron dengan palet terbaru
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
            return existing;

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }

        Debug.LogError("[Praktikum06] Tidak ada slot Layer kosong untuk '" + layerName + "'.");
        return 0;
    }

    private static SerializedProperty FindProp(SerializedObject so, Object target, string field)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
            Debug.LogError("[Praktikum06] Field tidak ditemukan: " + target.GetType().Name + "." + field);
        return prop;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRefArray(Object target, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStringArray(Object target, string field, string[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).stringValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string field, string value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetColor(Object target, string field, Color value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string field, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string field, int value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMask(Object target, string field, int mask)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = FindProp(so, target, field);
        if (prop == null) return;
        prop.intValue = mask;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
