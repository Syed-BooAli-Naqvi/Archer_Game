using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Plays the level until the boss is defeated, at different upgrade levels,
// and measures how challenging / eventful the run was.
// The player is given effectively infinite HP so every run reaches the end;
// the moment a real player (100 HP) would have died is recorded instead.
public class BossRunTest
{
    const float MaxRealSeconds = 360f;
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    const float GodHealth = 1_000_000f;
    const float Speed = 3f; // game runs 3x faster than real time during the test

    static readonly string[] PrefKeys =
    {
        "Cash", "Upgrade_Income", "Upgrade_Fire_Rate", "Upgrade_Arrow", "UpgradeDoneOnce", "GameRunCount", "MusicLevel", "SoundLevel"
    };

    [UnityTest, Timeout(480000)]
    public IEnumerator A_NoUpgrades() => Wrap("A_NoUpgrades", 0, 0);

    [UnityTest, Timeout(480000)]
    public IEnumerator B_MidUpgrades() => Wrap("B_MidUpgrades", 10, 10);

    [UnityTest, Timeout(480000)]
    public IEnumerator C_HighUpgrades() => Wrap("C_HighUpgrades", 20, 20);

    // ---------------------------------------------------------------------

    StringBuilder report;
    List<string> errors;
    List<string> warnings;
    string outDir, runName;
    int shotIndex;

    static Type T(string name) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name == "Assembly-CSharp")
            .Select(a => a.GetType(name))
            .FirstOrDefault(t => t != null);

    static object Get(object obj, string member)
    {
        Type type = obj as Type ?? obj.GetType();
        object target = obj is Type ? null : obj;
        FieldInfo f = type.GetField(member, All);
        if (f != null) return f.GetValue(target);
        return type.GetProperty(member, All)?.GetValue(target);
    }

    static void Set(object obj, string member, object value) =>
        obj.GetType().GetField(member, All).SetValue(obj, value);

    static object Call(object obj, string method, params object[] args)
    {
        Type type = obj as Type ?? obj.GetType();
        object target = obj is Type ? null : obj;
        return type.GetMethods(All).First(x => x.Name == method && x.GetParameters().Length == args.Length).Invoke(target, args);
    }

    static List<Component> FindAll(Type t) =>
        UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude, FindObjectsSortMode.None).Cast<Component>().ToList();

    void Log(string line)
    {
        report.AppendLine(line);
        Debug.Log($"[PLAYTEST {runName}] {line}");
    }

    void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            string first = stack?.Split('\n').FirstOrDefault(s => s.Contains("Assets/")) ?? "";
            errors.Add($"{type}: {condition} {first}".Trim());
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition.Split('\n')[0]);
        }
    }

    void Screenshot(string label)
    {
        try
        {
            Camera cam = Camera.allCameras.Where(c => c.isActiveAndEnabled).OrderByDescending(c => c.depth).FirstOrDefault();
            if (cam == null) return;
            const int w = 540, h = 960;
            var rt = new RenderTexture(w, h, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(outDir, $"{runName}_{++shotIndex:00}_{label}.png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
        }
        catch (Exception e)
        {
            Log($"Screenshot '{label}' failed: {e.Message}");
        }
    }

    // Full Game view capture, including UI canvases. Must be called right after WaitForEndOfFrame.
    void CaptureUI(string label)
    {
        try
        {
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            Color32[] px = tex.GetPixels32();
            long sum = 0;
            for (int i = 0; i < px.Length; i += 97) sum += px[i].r + px[i].g + px[i].b;
            bool blank = sum / Mathf.Max(1, px.Length / 97) < 5;
            if (!blank)
                File.WriteAllBytes(Path.Combine(outDir, $"{runName}_{++shotIndex:00}_{label}_UI.png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            if (blank) Screenshot(label);
        }
        catch (Exception e)
        {
            Log($"UI capture '{label}' failed ({e.Message}), using camera render");
            Screenshot(label);
        }
    }

    static string StateName(Animator a)
    {
        if (a == null || !a.isActiveAndEnabled || a.runtimeAnimatorController == null) return null;
        var info = a.GetCurrentAnimatorStateInfo(0);
        foreach (string n in new[] { "Running", "Idle", "Sword Attack", "Dying", "Bow Attack" })
            if (info.IsName(n)) return n;
        return "other";
    }

    // Visual radius on the ground plane from the character's mesh renderers (particles excluded).
    static float VisualRadius(Component c)
    {
        var rends = c.GetComponentsInChildren<Renderer>(false)
            .Where(x => x.enabled && (x is SkinnedMeshRenderer || x is MeshRenderer) && x.gameObject.name != "Boss Charge Marker").ToList();
        if (rends.Count == 0) return 0.5f;
        Bounds b = rends[0].bounds;
        foreach (var x in rends) b.Encapsulate(x.bounds);
        return Mathf.Max(b.extents.x, b.extents.z);
    }

    static Animator MainAnimator(Component c, string controllerName)
    {
        var all = c.GetComponentsInChildren<Animator>(false).Where(a => a.isActiveAndEnabled && a.runtimeAnimatorController != null).ToList();
        return all.FirstOrDefault(a => a.runtimeAnimatorController.name == controllerName) ?? all.FirstOrDefault();
    }

    IEnumerator Wrap(string name, int arrowUpgrades, int fireRateUpgrades)
    {
        runName = name;
        report = new StringBuilder();
        errors = new List<string>();
        warnings = new List<string>();
        shotIndex = 0;
        outDir = Path.Combine(Application.dataPath, "../Temp/PlayTest");
        Directory.CreateDirectory(outDir);

        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceived += OnLog;

        var backup = new Dictionary<string, (bool has, int i, float f)>();
        foreach (string k in PrefKeys)
            backup[k] = (PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k), PlayerPrefs.GetFloat(k));

        // Upgrade level for this run (read by UpgradeManager.Awake when the scene loads).
        PlayerPrefs.SetInt("Upgrade_Arrow", arrowUpgrades);
        PlayerPrefs.SetInt("Upgrade_Fire_Rate", fireRateUpgrades);
        PlayerPrefs.SetInt("UpgradeDoneOnce", 1);
        PlayerPrefs.Save();

        try
        {
            IEnumerator run = Run();
            Exception failure = null;
            while (true)
            {
                object current;
                try
                {
                    if (!run.MoveNext()) break;
                    current = run.Current;
                }
                catch (Exception e)
                {
                    failure = e.InnerException ?? e;
                    Log($"TEST STOPPED: {failure.GetType().Name}: {failure.Message}\n{failure.StackTrace}");
                    break;
                }
                yield return current;
            }
            if (failure != null) throw failure;
        }
        finally
        {
            Time.timeScale = 1f;
            Application.logMessageReceived -= OnLog;
            foreach (var kv in backup)
            {
                PlayerPrefs.DeleteKey(kv.Key);
                if (!kv.Value.has) continue;
                if (kv.Key == "MusicLevel" || kv.Key == "SoundLevel") PlayerPrefs.SetFloat(kv.Key, kv.Value.f);
                else PlayerPrefs.SetInt(kv.Key, kv.Value.i);
            }
            PlayerPrefs.Save();

            Log($"Errors/exceptions logged: {errors.Count}");
            foreach (var g in errors.GroupBy(e => e).Take(20))
                Log($"  {g.Count()}x {g.Key}");
            Log($"Warnings logged: {warnings.Count}");
            foreach (var g in warnings.GroupBy(w => w).OrderByDescending(g => g.Count()).Take(15))
                Log($"  {g.Count()}x {g.Key}");
            File.WriteAllText(Path.Combine(outDir, $"report_{runName}.txt"), report.ToString());
        }
    }

    class EnemyRecord
    {
        public float spawned, died = -1, closest = float.MaxValue;
        public bool reachedPlayer;
    }

    IEnumerator Run()
    {
        Type gameState = T("ArcherGameState");
        Type uiType = T("MainMenuUIHandler");
        Type enemyHealthT = T("EnemyHealth");
        Type bossHealthT = T("BossHealth");
        Type playerHealthT = T("ArcherPlayerHealth");
        Type arrowCtrlT = T("ArcherArrowController");
        Type archerT = T("ArcherController");
        Type upgradeT = T("UpgradeManager");
        Type spawnerT = T("EnemySpawner");

        // Clean boot: previous runs leave DontDestroyOnLoad managers and static game state behind,
        // which would stop the Loading scene from loading the gameplay scene again.
        Call(gameState, "StopGame");
        foreach (string persistent in new[] { "LoadingScript", "SoundManager" })
            foreach (var c in UnityEngine.Object.FindObjectsByType(T(persistent), FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>())
                UnityEngine.Object.Destroy(c.gameObject);
        Time.timeScale = 1f;
        yield return null;

        SceneManager.LoadScene("My Archery Loading");
        float r0 = Time.realtimeSinceStartup;
        while (SceneManager.GetActiveScene().name != "My Archery Loading" && Time.realtimeSinceStartup - r0 < 10f)
            yield return null;
        while (SceneManager.GetActiveScene().name != "My Archery Gameplay" && Time.realtimeSinceStartup - r0 < 30f)
            yield return null;
        Assert.AreEqual("My Archery Gameplay", SceneManager.GetActiveScene().name, "Gameplay scene never loaded");
        yield return new WaitForSecondsRealtime(1f);

        object ui = Get(uiType, "Instance");
        object upgrades = Get(upgradeT, "Instance");
        Log($"Upgrades: arrows per shot = {Get(upgrades, "ArrowCount")}, fire rate = x{Get(upgrades, "FireRate")}");

        yield return new WaitForEndOfFrame();
        CaptureUI("main_menu");

        Call(ui, "TapToStart");
        r0 = Time.realtimeSinceStartup;
        while (!(bool)Get(gameState, "IsGameRunning") && Time.realtimeSinceStartup - r0 < 10f)
            yield return null;
        Assert.IsTrue((bool)Get(gameState, "IsGameRunning"), "Game never started");
        yield return null;

        var arrowCtrl = FindAll(arrowCtrlT).First();
        Log($"Shooting: {Get(arrowCtrl, "arrowsToShoot")} arrows every {(float)Get(arrowCtrl, "timeBetweenShots"):F2}s");
        var playerHealth = FindAll(playerHealthT).First();
        Transform player = (Transform)Get(arrowCtrl, "player");
        float minX = (float)Get(arrowCtrl, "minPlayerX"), maxX = (float)Get(arrowCtrl, "maxPlayerX");

        // God mode, so the run always reaches the boss.
        Set(playerHealth, "maxHealth", GodHealth);
        Set(playerHealth, "currentHealth", GodHealth);
        const float realMaxHp = 100f;

        var spawners = FindAll(spawnerT);
        var spawnerFired = spawners.ToDictionary(s => s, s => false);
        var enemies = new Dictionary<int, EnemyRecord>();
        var deathPos = new Dictionary<int, Vector3>();
        var deadMovers = new HashSet<int>();
        GameObject completePanel = (GameObject)Get(ui, "completePanel");

        float g0 = Time.time;
        float idleTime = 0f, bossSpawnT = -1f, bossDeadT = -1f, wouldHaveDiedAt = -1f;
        float damageBeforeBoss = 0f, damageDuringBoss = 0f, lastDamage = 0f, damageFromCharge = 0f, damageFromMinions = 0f;
        float nextSample = 0f;
        float maxBossHp = 0f;
        Component boss = null;
        string lastArrowType = "";
        int lastLoaded = -1, lastFired = -1;
        Type boosterT = T("BoosterPickup");
        var timeline = new List<string>();
        bool won = false;
        float telegraphStart = -1f;
        int charges = 0;
        float power = (float)Call(T("DifficultySettings"), "PlayerPower");
        Log($"Player power: x{Mathf.Max(1f, power):F1}");
        Log($"(Test runs the game at {Speed}x speed; all times below are game time.)");
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Time.timeScale = Speed;

        // ----- visual / animation / sound checks -----
        Type soundManagerT = T("SoundManager");
        Type playerCtrlT = T("ArcherPlayerController");
        var playerCtrl = FindAll(playerCtrlT).FirstOrDefault();
        Animator playerAnim = playerCtrl != null ? (Animator)Get(playerCtrl, "animator") : null;
        var prevPos = new Dictionary<int, Vector3>();
        var sliding = new Dictionary<string, int>();      // "who: state" -> samples moving fast in a non-moving animation
        int enemyRunInPlace = 0, enemyStillSamples = 0, bossRunInPlace = 0, bossStillSamples = 0;
        int playerSideSlide = 0, maxStacked = 0, stackedSamples = 0, visSamples = 0, maxBehind = 0, behindSamples = 0;
        int maxPlayingSounds = 0, maxListeners = 0;
        int maxParticleSystems = 0, maxLiveParticles = 0, maxFinishedEffects = 0;
        float enemyR = -1f, bossR = -1f, playerR = VisualRadius(player);
        int maxEnemyOverlapPairs = 0, enemyOverlapSamples = 0, maxCoveringPlayer = 0, coveringPlayerSamples = 0;
        int bossOnPlayerSamples = 0, maxEnemiesInBoss = 0, enemiesInBossSamples = 0;
        float minEnemyGap = float.MaxValue, minEnemyToPlayer = float.MaxValue, minBossToPlayer = float.MaxValue;
        int maxOffscreen = 0, offscreenSamples = 0;
        EdgeProbe edgeProbe = null;
        int popInVisible = 0, popInUnderHud = 0, onGateDuringBoss = 0, bossStoppedSamples = 0, maxPlayingSystems = 0;
        Type gateT = T("PowerZoneTrigger");
        var playerSlider = (UnityEngine.UI.Slider)Get(playerHealth, "healthSlider");
        {
            Camera cam = Camera.main;
            Ray top = cam.ViewportPointToRay(new Vector3(0.5f, 1f, 0f));
            if (new Plane(Vector3.up, player.position).Raycast(top, out float hd))
                Log($"Screen top edge is {top.GetPoint(hd).z - player.position.z:F1}m ahead of the player");
        }
        float maxModelDrift = 0f, maxModelDriftBoss = 0f, maxModelTurn = 0f, maxModelTurnBoss = 0f, maxBarOffset = 0f;
        Transform playerModel = playerAnim != null ? playerAnim.transform : null;
        Canvas playerBar = player.GetComponentInChildren<Canvas>();
        Log($"Player animator: applyRootMotion={(playerAnim != null ? playerAnim.applyRootMotion.ToString() : "?")}, humanoid={(playerAnim != null && playerAnim.isHuman)}");
        var finishedEffectNames = new Dictionary<string, int>();
        var soundCounts = new Dictionary<string, int>();
        var seenSoundObjects = new HashSet<int>();
        float nextVisual = 0f, prevPlayerX = player.position.x;
        string pendingShot = null;
        bool chargeShotTaken = false;
        float boosterReadyAt = -1f;
        var visualEvents = new HashSet<string>();
        int crowdRecord = 0;
        float crowdShotAt = -100f;
        Type enemyAIType = T("EnemyAI");
        int nextShotAt = 15;
        float realStart = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - realStart < MaxRealSeconds)
        {
            float t = Time.time - g0;
            float dt = Time.deltaTime;

            // ----- enemies -----
            var alive = new List<Component>();
            foreach (var e in FindAll(enemyHealthT))
            {
                int id = e.GetInstanceID();
                if (!enemies.TryGetValue(id, out var rec))
                {
                    enemies[id] = rec = new EnemyRecord { spawned = t };
                    Vector3 vp = Camera.main.WorldToViewportPoint(e.transform.position);
                    if (vp.z > 0 && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1)
                    {
                        popInVisible++;
                        if (vp.y > 0.85f && (vp.x < 0.25f || vp.x > 0.65f)) popInUnderHud++;
                    }
                }
                bool dead = (bool)Get(e, "isDead");
                if (dead)
                {
                    if (rec.died < 0) { rec.died = t; deathPos[id] = e.transform.position; }
                    else if (deathPos.TryGetValue(id, out Vector3 dp) && Vector3.Distance(dp, e.transform.position) > 0.3f) deadMovers.Add(id);
                    continue;
                }
                alive.Add(e);
                Vector3 d = e.transform.position - player.position; d.y = 0;
                rec.closest = Mathf.Min(rec.closest, d.magnitude);
                if (d.magnitude <= 2f) rec.reachedPlayer = true;
            }
            // Enemies destroyed without a death flag being seen still count as dead.
            foreach (var kv in enemies.Where(kv => kv.Value.died < 0))
                if (!alive.Any(a => a.GetInstanceID() == kv.Key)) kv.Value.died = t;

            if (boss == null)
            {
                boss = FindAll(bossHealthT).FirstOrDefault();
                if (boss != null)
                {
                    bossSpawnT = t;
                    maxBossHp = (float)Get(boss, "maxHealth");
                    timeline.Add($"t={t,6:F1}s  BOSS appears ({maxBossHp} HP)");
                    pendingShot = "boss_appears";
                }
            }
            bool bossAlive = boss != null && boss && boss.gameObject.activeInHierarchy && !(bool)Call(boss, "IsDead");

            if (alive.Count == 0 && !bossAlive) idleTime += dt;

            // ----- spawners -----
            foreach (var s in spawners)
            {
                if (spawnerFired[s] || s == null || !(bool)Get(s, "hasTriggered")) continue;
                spawnerFired[s] = true;
                timeline.Add($"t={t,6:F1}s  wave spawned: {Get(s, "enemyCount")} {((bool)Get(s, "isBoss") ? "boss" : "enemies")} " +
                             $"(enemies still alive from before: {alive.Count(a => enemies[a.GetInstanceID()].spawned < t)})");
            }

            // ----- damage -----
            float damage = GodHealth - (float)Get(playerHealth, "currentHealth");
            float newDamage = damage - lastDamage;
            if (newDamage > 0)
            {
                if (bossSpawnT >= 0) damageDuringBoss += newDamage; else damageBeforeBoss += newDamage;
                if (bossSpawnT >= 0)
                {
                    Component bai = boss ? boss.GetComponent(T("BossAI")) : null;
                    string st = bai != null ? Get(bai, "chargeState")?.ToString() : "";
                    if (st == "Charging" || st == "Recover") damageFromCharge += newDamage; else damageFromMinions += newDamage;
                }
                if (wouldHaveDiedAt < 0 && damage >= realMaxHp)
                {
                    wouldHaveDiedAt = t;
                    timeline.Add($"t={t,6:F1}s  ** a real player (100 HP) would have DIED here **");
                }
            }
            lastDamage = damage;
            if (playerSlider != null) playerSlider.value = Mathf.Max(15f, 100f - damage % 100f);

            // ----- booster: shot down -> loaded; a player taps the button shortly after -----
            int loaded = (int)Get(boosterT, "Loaded"), fired = (int)Get(boosterT, "Fired");
            object boosterMgr = Get(boosterT, "Instance");
            if (lastLoaded >= 0 && loaded > lastLoaded)
            {
                timeline.Add($"t={t,6:F1}s  booster collected -> 2X button loaded");
                pendingShot ??= "booster_button_ready";
                boosterReadyAt = t;
            }
            if (boosterMgr != null && boosterReadyAt >= 0 && t - boosterReadyAt > 1.5f && (int)Get(boosterMgr, "charges") > 0)
            {
                boosterReadyAt = -1f;
                Call(boosterMgr, "OnButtonPressed");
                timeline.Add($"t={t,6:F1}s  booster button pressed: 2 volleys of {(int)Get(arrowCtrl, "arrowsToShoot") * 2} arrows");
                pendingShot ??= "booster_fired";
            }
            lastLoaded = loaded;
            lastFired = fired;

            // ----- arrow type -----
            object archer = Get(archerT, "Instance");
            string arrowType = archer != null ? Get(archer, "currentArrowType").ToString() : "?";
            if (arrowType != lastArrowType)
            {
                timeline.Add($"t={t,6:F1}s  arrow type -> {arrowType}");
                lastArrowType = arrowType;
            }

            // ----- steering: line up with the nearest threat, otherwise sweep to pass gates -----
            // Boss charge warning: after a human-like reaction delay, steer out of the marked spot.
            Component bossAI = bossAlive ? boss.GetComponent(T("BossAI")) : null;
            bool telegraphing = bossAI != null && (bool)(Get(bossAI, "IsTelegraphing") ?? false);
            bool charging = bossAI != null && Get(bossAI, "chargeState")?.ToString() == "Charging";
            if (telegraphing && telegraphStart < 0) { telegraphStart = t; charges++; }
            if (telegraphing && !chargeShotTaken && t - telegraphStart > 0.3f) { chargeShotTaken = true; pendingShot = "boss_charge_warning"; }
            if (!telegraphing) telegraphStart = -1f;

            if (Time.timeScale > 0f)
            {
                Transform threat = bossAlive ? boss.transform
                    : alive.OrderBy(a => (a.transform.position - player.position).sqrMagnitude).FirstOrDefault()?.transform;
                float targetX = threat != null
                    ? Mathf.Clamp(threat.position.x, minX, maxX)
                    : Mathf.Lerp(minX, maxX, 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / 7f));
                // Like a player: grab a booster when no enemy is close.
                GameObject boosterObj = GameObject.Find("Booster Target");
                if (boosterObj != null && boosterObj.transform.position.z < player.position.z + 9f) Event("booster_target_close");
                GameObject coinBucket = GameObject.Find("Coin Bundle");
                if (coinBucket != null && coinBucket.transform.position.z < player.position.z + 8f) Event("coin_bucket_close");
                float nearestEnemy = alive.Count > 0 ? alive.Min(a => Vector3.Distance(a.transform.position, player.position)) : 99f;
                Component boosterTarget = boosterObj != null ? boosterObj.GetComponent(T("BoosterTarget")) : null;
                bool boosterReady = boosterTarget != null && (bool)Get(boosterTarget, "IsReady");
                if (boosterObj != null && (boosterReady || nearestEnemy > 6f) && boosterObj.transform.position.z > player.position.z - 1f)
                    targetX = Mathf.Clamp(boosterObj.transform.position.x, minX, maxX);
                if (boosterReady) Event("booster_ready_to_grab");

                if ((telegraphing && t - telegraphStart >= 0.35f) || charging)
                {
                    float markX = ((Vector3)Get(bossAI, "ChargeTarget")).x;
                    targetX = markX > (minX + maxX) * 0.5f ? markX - 3f : markX + 3f;
                    targetX = Mathf.Clamp(targetX, minX, maxX);
                }
                Vector3 p = player.position;
                p.x = Mathf.MoveTowards(p.x, targetX, 6f * dt);
                player.position = p;
            }

            if (t >= nextSample)
            {
                nextSample += 5f;
                float nearest = alive.Count > 0 ? alive.Min(a => Vector3.Distance(a.transform.position, player.position)) : -1;
                Log($"t={t,6:F1}s alive={alive.Count,2} nearestEnemy={(nearest < 0 ? "  -  " : nearest.ToString("F1") + "m")} " +
                    $"damageTaken={damage,4:F0} kills={enemies.Values.Count(e => e.died >= 0)}" +
                    (bossAlive ? $" BOSS hp={(float)Get(boss, "currentHealth"):F0}/{maxBossHp:F0} dist={Vector3.Distance(boss.transform.position, player.position):F1}m" : ""));
            }
            if (t >= nextShotAt) { pendingShot ??= $"t{nextShotAt}"; nextShotAt += 15; }

            // Event screenshots for visual review (each taken once).
            void Event(string name) { if (pendingShot == null && visualEvents.Add(name)) pendingShot = name; }
            if (alive.Any(e => { var ai = e.GetComponent(enemyAIType); return ai != null && Get(ai, "activeEffect").ToString() == "Freeze"; })) Event("first_frozen_enemy");
            if (alive.Any(e => { var ai = e.GetComponent(enemyAIType); return ai != null && Get(ai, "activeEffect").ToString() == "Fire"; })) Event("first_burning_enemy");
            if (enemies.Values.Any(e => e.died >= 0 && t - e.died > 0.15f && t - e.died < 0.5f)) Event("first_enemy_death");
            if (enemies.Values.Count(e => e.died >= 0 && t - e.died < 0.6f) >= 3) Event("multi_kill");
            if (alive.Count > crowdRecord + 5 && t - crowdShotAt > 10f) { crowdRecord = alive.Count; crowdShotAt = t; pendingShot ??= $"crowd_{alive.Count}_enemies"; }
            if (bossAlive && (float)Get(boss, "currentHealth") < maxBossHp * 0.5f) Event("boss_half_hp");

            // ----- visual / animation / sound sampling (every 0.25s of game time) -----
            if (t >= nextVisual)
            {
                float since = t - (nextVisual - 0.25f);
                nextVisual = t + 0.25f;
                visSamples++;

                void CheckMover(Component c, string who, string controller, ref int runInPlace, ref int still)
                {
                    int id = c.GetInstanceID();
                    Vector3 pos = c.transform.position;
                    if (prevPos.TryGetValue(id, out Vector3 before) && since > 0.01f)
                    {
                        Vector3 d = pos - before; d.y = 0;
                        float speed = d.magnitude / since;
                        Animator anim = MainAnimator(c, controller);
                        string state = StateName(anim);
                        bool inTransition = anim != null && anim.IsInTransition(0);
                        if (state != null && !inTransition)
                        {
                            if (speed > 0.8f && state != "Running" && state != "Dying")
                            {
                                string key = $"{who} moves while playing '{state}'";
                                sliding[key] = sliding.TryGetValue(key, out int n) ? n + 1 : 1;
                            }
                            if (speed < 0.15f)
                            {
                                still++;
                                if (state == "Running") runInPlace++;
                            }
                        }
                    }
                    prevPos[id] = pos;
                }

                foreach (var e in alive) CheckMover(e, "Enemy", "Enemy", ref enemyRunInPlace, ref enemyStillSamples);
                if (bossAlive) CheckMover(boss, "Boss", "Boss", ref bossRunInPlace, ref bossStillSamples);

                // Player sliding sideways while standing idle (after the platform stops at the boss).
                if (playerAnim != null && Mathf.Abs(player.position.x - prevPlayerX) / Mathf.Max(since, 0.01f) > 1f && StateName(playerAnim) == "Idle")
                    playerSideSlide++;
                prevPlayerX = player.position.x;

                // Spacing between characters, using their real model sizes.
                if (enemyR < 0 && alive.Count > 0) enemyR = alive.Take(5).Max(a => VisualRadius(a));
                if (bossR < 0 && bossAlive) bossR = VisualRadius(boss);
                if (enemyR > 0)
                {
                    int overlapPairs = 0;
                    for (int i = 0; i < alive.Count; i++)
                        for (int j = i + 1; j < alive.Count; j++)
                        {
                            Vector3 d = alive[i].transform.position - alive[j].transform.position; d.y = 0;
                            minEnemyGap = Mathf.Min(minEnemyGap, d.magnitude);
                            if (d.magnitude < 1.5f * enemyR) overlapPairs++;   // models overlap by more than a quarter
                        }
                    maxEnemyOverlapPairs = Mathf.Max(maxEnemyOverlapPairs, overlapPairs);
                    if (overlapPairs > 0) enemyOverlapSamples++;

                    int covering = 0;
                    foreach (var a in alive)
                    {
                        Vector3 d = a.transform.position - player.position; d.y = 0;
                        minEnemyToPlayer = Mathf.Min(minEnemyToPlayer, d.magnitude);
                        if (d.magnitude < (enemyR + playerR) * 0.7f) covering++;
                    }
                    maxCoveringPlayer = Mathf.Max(maxCoveringPlayer, covering);
                    if (covering > 0) coveringPlayerSamples++;
                    if (alive.Count(a => Vector3.Distance(a.transform.position, player.position) < 2.2f) >= 4) Event("player_surrounded");
                }
                if (bossAlive && bossR > 0)
                {
                    Vector3 d = boss.transform.position - player.position; d.y = 0;
                    minBossToPlayer = Mathf.Min(minBossToPlayer, d.magnitude);
                    if (d.magnitude < (bossR + playerR) * 0.7f) bossOnPlayerSamples++;
                    if (d.magnitude < bossR + playerR + 0.5f) Event("boss_closest_to_player");
                    if (enemyR > 0)
                    {
                        int inBoss = alive.Count(a => { Vector3 e = a.transform.position - boss.transform.position; e.y = 0; return e.magnitude < (bossR + enemyR) * 0.7f; });
                        maxEnemiesInBoss = Mathf.Max(maxEnemiesInBoss, inBoss);
                        if (inBoss > 0) enemiesInBossSamples++;
                        if (alive.Count(a => Vector3.Distance(a.transform.position, boss.transform.position) < bossR + 2f) >= 2) Event("boss_with_minions");
                    }
                }

                // Gate strip under the player once the ground has stopped for the boss.
                if (bossAlive && bossSpawnT >= 0 && t - bossSpawnT > 8f)
                {
                    bossStoppedSamples++;
                    bool onGate = false;
                    foreach (var gate in FindAll(gateT))
                    {
                        Transform strip = gate.transform.parent != null ? gate.transform.parent : gate.transform;
                        foreach (var rr in strip.GetComponentsInChildren<Renderer>())
                            if (player.position.z > rr.bounds.min.z && player.position.z < rr.bounds.max.z) onGate = true;
                    }
                    if (onGate) onGateDuringBoss++;
                }
                // Enemies on screen vertically but cut off past the left/right edges,
                // measured at the end of the frame (after all game LateUpdates), i.e. what is actually drawn.
                if (edgeProbe == null)
                {
                    edgeProbe = new GameObject("Edge Probe").AddComponent<EdgeProbe>();
                    EdgeProbe.Counter = () => FindAll(enemyHealthT).Count(a =>
                    {
                        if ((bool)Get(a, "isDead")) return false;
                        Vector3 v = Camera.main.WorldToViewportPoint(a.transform.position);
                        return v.z > 0 && v.y > 0 && v.y < 1 && (v.x < 0.05f || v.x > 0.95f);
                    });
                }
                int offscreen = EdgeProbe.Last;
                maxOffscreen = Mathf.Max(maxOffscreen, offscreen);
                if (offscreen > 0) { offscreenSamples++; Event("enemies_at_screen_edge"); }
                maxPlayingSystems = Mathf.Max(maxPlayingSystems, UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(ps => ps.isPlaying));

                // Player model vs player object / health bar: drift and facing.
                if (playerModel != null)
                {
                    Vector3 drift = playerModel.position - player.position; drift.y = 0;
                    Transform hips = playerAnim.isHuman ? playerAnim.GetBoneTransform(HumanBodyBones.Hips) : null;
                    Vector3 fwd = hips != null ? hips.forward : playerModel.forward; fwd.y = 0;
                    float turn = Mathf.Abs(Vector3.SignedAngle(Vector3.forward, fwd, Vector3.up));
                    Vector3 bodyPos = hips != null ? hips.position : playerModel.position;
                    if (playerBar != null) { Vector3 b = playerBar.transform.position - bodyPos; maxBarOffset = Mathf.Max(maxBarOffset, Mathf.Abs(b.x)); }
                    if (bossAlive) { maxModelDriftBoss = Mathf.Max(maxModelDriftBoss, drift.magnitude); maxModelTurnBoss = Mathf.Max(maxModelTurnBoss, turn); }
                    else { maxModelDrift = Mathf.Max(maxModelDrift, drift.magnitude); maxModelTurn = Mathf.Max(maxModelTurn, turn); }
                    if (bossAlive && (drift.magnitude > 0.5f || turn > 45f)) Event("player_model_off");
                }

                // Enemies behind the player can't be hit (arrows only fly forward).
                int behind = alive.Count(a => a.transform.position.z < player.position.z - 0.2f);
                maxBehind = Mathf.Max(maxBehind, behind);
                if (behind > 0) behindSamples++;

                // Enemies stacked inside each other (< 0.4m apart).
                int stacked = 0;
                for (int i = 0; i < alive.Count; i++)
                    for (int j = i + 1; j < alive.Count; j++)
                    {
                        Vector3 d = alive[i].transform.position - alive[j].transform.position; d.y = 0;
                        if (d.sqrMagnitude < 0.16f) stacked++;
                    }
                maxStacked = Mathf.Max(maxStacked, stacked);
                if (stacked > 0) stackedSamples++;

                // Particles: how many systems/particles exist, and finished effects never cleaned up.
                var systems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                maxParticleSystems = Mathf.Max(maxParticleSystems, systems.Length);
                maxLiveParticles = Mathf.Max(maxLiveParticles, systems.Sum(ps => ps.particleCount));
                var finished = systems.Select(ps => ps.transform.root.gameObject).Distinct()
                    .Where(go => go.name.EndsWith("(Clone)") && go.GetComponent<ParticleSystem>() != null && !go.GetComponent<ParticleSystem>().IsAlive(true))
                    .ToList();
                maxFinishedEffects = Mathf.Max(maxFinishedEffects, finished.Count);
                foreach (var go in finished) finishedEffectNames[go.name] = Mathf.Max(finishedEffectNames.TryGetValue(go.name, out int fc) ? fc : 0, finished.Count(f => f.name == go.name));

                // Sounds.
                object sm = Get(soundManagerT, "Instance");
                if (sm is Component smc && smc)
                {
                    var sources = smc.GetComponentsInChildren<AudioSource>();
                    maxPlayingSounds = Mathf.Max(maxPlayingSounds, sources.Count(a => a.isPlaying));
                    foreach (var a in sources)
                    {
                        if (a.gameObject == smc.gameObject || !seenSoundObjects.Add(a.gameObject.GetInstanceID())) continue;
                        string n = a.gameObject.name;
                        soundCounts[n] = soundCounts.TryGetValue(n, out int c) ? c + 1 : 1;
                    }
                }
                maxListeners = Mathf.Max(maxListeners, UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled));
            }

            if (completePanel && completePanel.activeInHierarchy)
            {
                won = true;
                bossDeadT = t;
                timeline.Add($"t={t,6:F1}s  BOSS DEFEATED - level complete");
                break;
            }

            if (pendingShot != null)
            {
                yield return new WaitForEndOfFrame();
                CaptureUI(pendingShot);
                pendingShot = null;
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.5f);
        yield return new WaitForEndOfFrame();
        CaptureUI(won ? "win_screen" : "timeout");

        // ----- summary -----
        var normal = enemies.Values.ToList();
        var killed = normal.Where(e => e.died >= 0).ToList();
        float total = bossDeadT >= 0 ? bossDeadT : Time.time - g0;

        Log("");
        Log("=== TIMELINE ===");
        foreach (var line in timeline) Log(line);
        Log("");
        Log($"=== SUMMARY: {(won ? "BOSS DEFEATED" : "DID NOT FINISH")} ===");
        Log($"Level length: {total:F1}s (boss fight {(bossSpawnT >= 0 ? (total - bossSpawnT).ToString("F1") + "s" : "n/a")})");
        Log($"Enemies: {normal.Count} spawned, {killed.Count} killed");
        if (killed.Count > 0)
        {
            Log($"Avg enemy lifetime: {killed.Average(e => e.died - e.spawned):F1}s  (shortest {killed.Min(e => e.died - e.spawned):F1}s, longest {killed.Max(e => e.died - e.spawned):F1}s)");
            Log($"Enemies that reached the player (<2m): {normal.Count(e => e.reachedPlayer)} of {normal.Count} ({100f * normal.Count(e => e.reachedPlayer) / normal.Count:F0}%)");
            Log($"Avg closest approach: {normal.Average(e => e.closest):F1}m");
        }
        Log($"Time with nothing to shoot at: {idleTime:F1}s of {total:F1}s ({100f * idleTime / Mathf.Max(total, 0.01f):F0}%)");
        Log($"Boss charges: {charges} (bot dodges after a 0.35s reaction delay)");
        Log($"Damage taken: {damageBeforeBoss:F0} before boss, {damageDuringBoss:F0} during boss (real HP is {realMaxHp})");
        Log($"  boss-phase damage from boss charges: {damageFromCharge:F0}, from enemies/minions: {damageFromMinions:F0}");
        Log(wouldHaveDiedAt >= 0 ? $"A real player would have died at t={wouldHaveDiedAt:F1}s" : "A real player would have survived");
        Log($"Dead enemies that kept moving after death: {deadMovers.Count}");

        // ----- reward check: coins are credited per kill, so xN should add (N-1) x the level reward -----
        if (won)
        {
            int levelReward = (int)Get(ui, "rewardAmountThisLevel");
            var rewardTexts = (Array)Get(ui, "rewardTxt");
            string shown = rewardTexts != null && rewardTexts.Length > 0 ? (string)Get(rewardTexts.GetValue(0), "text") : "?";
            int mult = (int)Call(ui, "GetXRewardMultiplier");
            int before = PlayerPrefs.GetInt("Cash");
            Call(ui, "ClaimXReward");
            int extra = PlayerPrefs.GetInt("Cash") - before;
            Log($"Reward screen shows \"{shown}\" for a level reward of {levelReward} -> {(shown == levelReward.ToString() ? "correct" : "WRONG")}");
            Log($"Claim x{mult}: +{extra} extra on top of {levelReward} already earned = {levelReward + extra} total -> {(levelReward + extra == levelReward * mult ? "correct" : "WRONG")}");
            yield return new WaitForSecondsRealtime(1f);
        }

        Log("");
        Log("=== VISUAL / ANIMATION / SOUND CHECKS ===");
        Log($"Sliding (moving fast while playing a non-moving animation): {(sliding.Count == 0 ? "none" : "")}");
        foreach (var kv in sliding.OrderByDescending(k => k.Value)) Log($"  {kv.Key}: {kv.Value} samples");
        Log($"Enemies standing still but playing 'Running' (running in place): {enemyRunInPlace} of {enemyStillSamples} still samples");
        Log($"Boss standing still but playing 'Running': {bossRunInPlace} of {bossStillSamples} still samples");
        Log($"Player sliding sideways while in 'Idle': {playerSideSlide} samples");
        Log($"Enemies cut off at the left/right screen edge: max {maxOffscreen} at once, in {offscreenSamples} of {visSamples} samples");
        Log($"Enemies visible on screen the moment they spawned (pop-in): {popInVisible} of {enemies.Count}, under the HUD corners: {popInUnderHud}");
        Log($"Player standing on a gate strip during the boss fight: {onGateDuringBoss} of {bossStoppedSamples} samples");
        Log($"Particle systems actually playing: max {maxPlayingSystems}");
        Type coinT = T("CoinTrail");
        if (coinT != null) Log($"Environment coins: {Get(coinT, "Collected")} collected of {Get(coinT, "Spawned")} spawned (cumulative this session)");
        Log($"Player model drift from Player object: max {maxModelDrift:F2}m before boss, {maxModelDriftBoss:F2}m during boss");
        Log($"Player body turned away from forward: max {maxModelTurn:F0} deg before boss, {maxModelTurnBoss:F0} deg during boss");
        Log($"Player health bar sideways offset from body: max {maxBarOffset:F2}m");
        Log($"Model sizes (radius): enemy {enemyR:F2}m, boss {bossR:F2}m, player {playerR:F2}m");
        Log($"Enemies visibly overlapping each other: max {maxEnemyOverlapPairs} pairs at once, in {enemyOverlapSamples} of {visSamples} samples (closest gap {minEnemyGap:F2}m)");
        Log($"Enemies covering the player model: max {maxCoveringPlayer} at once, in {coveringPlayerSamples} samples (closest {minEnemyToPlayer:F2}m)");
        Log($"Boss overlapping the player: {bossOnPlayerSamples} samples (closest {(minBossToPlayer == float.MaxValue ? "-" : minBossToPlayer.ToString("F2") + "m")})");
        Log($"Enemies inside the boss model: max {maxEnemiesInBoss} at once, in {enemiesInBossSamples} samples");
        Log($"Enemies behind the player (unhittable): max {maxBehind} at once, in {behindSamples} of {visSamples} samples");
        Log($"Enemies stacked inside each other (<0.4m): max {maxStacked} pairs at once, in {stackedSamples} of {visSamples} samples");
        Log($"Particles: max {maxParticleSystems} particle systems in scene, max {maxLiveParticles} live particles");
        Log($"Finished effects never cleaned up (leak): max {maxFinishedEffects} at once");
        foreach (var kv in finishedEffectNames.OrderByDescending(k => k.Value).Take(5)) Log($"  {kv.Key}: up to {kv.Value}");
        Log($"Sounds: max {maxPlayingSounds} playing at once; active AudioListeners max {maxListeners}");
        foreach (var kv in soundCounts.OrderByDescending(k => k.Value))
            Log($"  {kv.Key}: played {kv.Value}x ({kv.Value / Mathf.Max(total, 1f) * 60f:F0} per minute)");
    }
}

// Samples a value at the very end of each frame's LateUpdate phase.
[DefaultExecutionOrder(32000)]
public class EdgeProbe : MonoBehaviour
{
    public static Func<int> Counter;
    public static int Last;

    void LateUpdate()
    {
        if (Counter != null) Last = Counter();
    }
}
