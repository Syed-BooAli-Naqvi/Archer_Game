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

// Automated play-through: loads the Loading scene like the real game,
// taps start, steers the archer left/right and records what happens.
// Game scripts live in Assembly-CSharp, so they are accessed via reflection.
public class ArcheryPlayTest
{
    const float MaxPlaySeconds = 150f;
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    static readonly string[] PrefKeys =
    {
        "Cash", "Upgrade_Income", "Upgrade_Fire_Rate", "Upgrade_Arrow", "UpgradeDoneOnce", "GameRunCount", "MusicLevel", "SoundLevel"
    };

    readonly StringBuilder report = new StringBuilder();
    readonly List<string> errors = new List<string>();
    string outDir;
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
        PropertyInfo p = type.GetProperty(member, All);
        return p?.GetValue(target);
    }

    static object Call(object obj, string method, params object[] args)
    {
        Type type = obj as Type ?? obj.GetType();
        object target = obj is Type ? null : obj;
        MethodInfo m = type.GetMethods(All).First(x => x.Name == method && x.GetParameters().Length == args.Length);
        return m.Invoke(target, args);
    }

    static UnityEngine.Object[] FindAll(Type t) =>
        UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude, FindObjectsSortMode.None);

    void Log(string line)
    {
        report.AppendLine(line);
        Debug.Log("[PLAYTEST] " + line);
    }

    void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            string first = stack?.Split('\n').FirstOrDefault(s => s.Contains("Assets/")) ?? "";
            lock (errors) errors.Add($"[{Time.realtimeSinceStartup:F1}s] {type}: {condition} {first}".Trim());
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
            string path = Path.Combine(outDir, $"{++shotIndex:00}_{label}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
        }
        catch (Exception e)
        {
            Log($"Screenshot '{label}' failed: {e.Message}");
        }
    }

    [UnityTest]
    [Timeout(400000)]
    public IEnumerator PlayThroughLevel()
    {
        outDir = Environment.GetEnvironmentVariable("ARCHERY_TEST_OUT");
        if (string.IsNullOrEmpty(outDir))
            outDir = Path.Combine(Application.dataPath, "../Temp/PlayTest");
        Directory.CreateDirectory(outDir);

        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceived += OnLog;

        // Back up save data so the test doesn't change the player's progress.
        var backup = new Dictionary<string, (bool has, int i, float f)>();
        foreach (string k in PrefKeys)
            backup[k] = (PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k), PlayerPrefs.GetFloat(k));

        try
        {
            // Step manually so an exception still gets written to the report.
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

            Log("");
            Log($"=== ERRORS / EXCEPTIONS ({errors.Count}) ===");
            foreach (var g in errors.GroupBy(e => e.Substring(e.IndexOf(']') + 1)).Take(40))
                Log($"{g.Count()}x {g.First()}");
            File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
        }
    }

    IEnumerator Run()
    {
        Type gameState = T("ArcherGameState");
        Type uiType = T("MainMenuUIHandler");
        Type enemyHealthT = T("EnemyHealth");
        Type enemyAIT = T("EnemyAI");
        Type bossHealthT = T("BossHealth");
        Type playerHealthT = T("ArcherPlayerHealth");
        Type arrowCtrlT = T("ArcherArrowController");
        Type arrowT = T("ArrowController");
        Type archerT = T("ArcherController");
        Type upgradeT = T("UpgradeManager");

        // ---------- Boot like the real game ----------
        float t0 = Time.realtimeSinceStartup;
        SceneManager.LoadScene("My Archery Loading");
        yield return null;
        Log("Loaded 'My Archery Loading'");
        Screenshot("loading");

        while (SceneManager.GetActiveScene().name != "My Archery Gameplay" && Time.realtimeSinceStartup - t0 < 30f)
            yield return null;
        Assert.AreEqual("My Archery Gameplay", SceneManager.GetActiveScene().name, "Gameplay scene never loaded");
        Log($"Gameplay scene active after {Time.realtimeSinceStartup - t0:F1}s");
        yield return new WaitForSecondsRealtime(1.5f);
        Screenshot("main_menu");

        object ui = Get(uiType, "Instance");
        Assert.IsNotNull(ui, "MainMenuUIHandler not found");
        object upgrades = Get(upgradeT, "Instance");
        Log($"Start cash: {PlayerPrefs.GetInt("Cash")}, income x{Get(upgrades, "Income")}, fireRate x{Get(upgrades, "FireRate")}, arrows {Get(upgrades, "ArrowCount")}");

        // ---------- Tap to start ----------
        Call(ui, "TapToStart");
        t0 = Time.realtimeSinceStartup;
        while (!(bool)Get(gameState, "IsGameRunning") && Time.realtimeSinceStartup - t0 < 10f)
            yield return null;
        Assert.IsTrue((bool)Get(gameState, "IsGameRunning"), "Game never started after TapToStart");
        Log($"Game running {Time.realtimeSinceStartup - t0:F1}s after TapToStart");
        yield return null;
        Screenshot("game_start");

        var arrowCtrl = (Component)FindAll(arrowCtrlT).FirstOrDefault();
        var playerHealth = (Component)FindAll(playerHealthT).FirstOrDefault();
        Transform player = arrowCtrl != null ? (Transform)Get(arrowCtrl, "player") : null;
        float minX = arrowCtrl != null ? (float)Get(arrowCtrl, "minPlayerX") : -3f;
        float maxX = arrowCtrl != null ? (float)Get(arrowCtrl, "maxPlayerX") : 3f;
        Log($"Player: {(player ? player.name : "NOT FOUND")}, steer range x {minX}..{maxX}");

        GameObject completePanel = (GameObject)Get(ui, "completePanel");
        GameObject failedPanel = (GameObject)Get(ui, "failedPanel");

        // ---------- Play ----------
        var deadEnemyPos = new Dictionary<int, (Vector3 pos, float time, string name)>();
        var deadMovers = new HashSet<string>();
        var deadAttackers = new HashSet<string>();
        int maxEnemies = 0, kills = 0, seenEnemies = 0;
        var seenIds = new HashSet<int>();
        float playStart = Time.realtimeSinceStartup;
        float nextSample = 0f, nextShot = 10f;
        string outcome = "timeout";
        float lastHp = playerHealth ? (float)Get(playerHealth, "currentHealth") : 0f;

        while (Time.realtimeSinceStartup - playStart < MaxPlaySeconds)
        {
            float t = Time.realtimeSinceStartup - playStart;

            // Steering: sweep left/right like a player swiping, to pass through gates.
            if (player != null && Time.timeScale > 0f)
            {
                float target = Mathf.Lerp(minX, maxX, 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / 7f));
                Vector3 p = player.position;
                p.x = Mathf.MoveTowards(p.x, target, 6f * Time.deltaTime);
                player.position = p;
            }

            // Track enemies, and whether dead ones keep moving / attacking.
            var enemies = FindAll(enemyHealthT).Cast<Component>().ToList();
            maxEnemies = Mathf.Max(maxEnemies, enemies.Count(e => !(bool)Get(e, "isDead")));
            foreach (var e in enemies)
            {
                int id = e.GetInstanceID();
                if (seenIds.Add(id)) seenEnemies++;
                if (!(bool)Get(e, "isDead")) continue;
                if (!deadEnemyPos.ContainsKey(id))
                {
                    deadEnemyPos[id] = (e.transform.position, t, e.name);
                    kills++;
                    continue;
                }
                var d = deadEnemyPos[id];
                Vector3 delta = e.transform.position - d.pos; delta.y = 0;
                if (delta.magnitude > 0.3f) deadMovers.Add($"{e.name}#{id} moved {delta.magnitude:F2}m after death");
            }

            // Detect attacks after death: lastAttackTime (game time) later than time of death.
            foreach (var e in enemies)
            {
                if (!(bool)Get(e, "isDead")) continue;
                var ai = e.GetComponent(enemyAIT);
                if (ai == null) continue;
                int id = e.GetInstanceID();
                if (!deathGameTime.ContainsKey(id)) deathGameTime[id] = Time.time;
                float lat = (float)Get(ai, "lastAttackTime");
                if (lat > deathGameTime[id] + 0.001f) deadAttackers.Add($"{e.name}#{id} attacked {lat - deathGameTime[id]:F2}s after death");
            }

            if (t >= nextSample)
            {
                nextSample += 2f;
                float hp = playerHealth ? (float)Get(playerHealth, "currentHealth") : -1;
                var boss = FindAll(bossHealthT).Cast<Component>().FirstOrDefault();
                int activeArrows = FindAll(arrowT).Length;
                object archer = Get(archerT, "Instance");
                string arrowType = archer != null ? Get(archer, "currentArrowType").ToString() : "?";
                Log($"t={t,5:F1}s z={(player ? player.position.z : 0),7:F1} x={(player ? player.position.x : 0),5:F1} hp={hp,5:F0}{(hp < lastHp ? "(-" + (lastHp - hp) + ")" : "")} " +
                    $"enemies={enemies.Count(e => !(bool)Get(e, "isDead"))} kills={kills} arrowsInFlight={activeArrows} arrowType={arrowType} " +
                    $"reward={Get(ui, "rewardAmountThisLevel")} cash={PlayerPrefs.GetInt("Cash")}" +
                    (boss ? $" BOSS hp={Get(boss, "currentHealth")}" : ""));
                lastHp = hp;
            }

            if (t >= nextShot) { nextShot += 15f; Screenshot($"play_{(int)t}s"); }

            if (completePanel && completePanel.activeInHierarchy) { outcome = "LEVEL COMPLETE"; break; }
            if (failedPanel && failedPanel.activeInHierarchy) { outcome = "PLAYER DIED"; break; }

            yield return null;
        }

        float playTime = Time.realtimeSinceStartup - playStart;
        yield return new WaitForSecondsRealtime(0.5f);
        Screenshot("end_" + outcome.Replace(' ', '_'));

        Log("");
        Log($"=== RESULT: {outcome} after {playTime:F1}s ===");
        Log($"Enemies seen: {seenEnemies}, killed: {kills}, max alive at once: {maxEnemies}");
        Log($"Dead enemies that kept moving: {deadMovers.Count}");
        foreach (var s in deadMovers.Take(10)) Log("  " + s);
        Log($"Dead enemies that kept attacking: {deadAttackers.Count}");
        foreach (var s in deadAttackers.Take(10)) Log("  " + s);

        // ---------- Reward flow ----------
        int rewardThisLevel = (int)Get(ui, "rewardAmountThisLevel");
        float income = (float)Get(upgrades, "Income");
        var rewardTexts = (Array)Get(ui, "rewardTxt");
        string shown = rewardTexts != null && rewardTexts.Length > 0 ? (string)Get(rewardTexts.GetValue(0), "text") : "?";
        Log($"Coins already credited during level: {rewardThisLevel} (income x{income}); end panel shows: \"{shown}\"");

        int before = PlayerPrefs.GetInt("Cash");
        if (outcome == "PLAYER DIED")
        {
            Call(ui, "Continue");
            Log($"Pressed Continue: cash {before} -> {PlayerPrefs.GetInt("Cash")} (extra +{PlayerPrefs.GetInt("Cash") - before} on top of coins already credited)");
        }
        else if (outcome == "LEVEL COMPLETE")
        {
            int mult = (int)Call(ui, "GetXRewardMultiplier");
            Call(ui, "ClaimXReward");
            Log($"Pressed Claim x{mult}: cash {before} -> {PlayerPrefs.GetInt("Cash")} (extra +{PlayerPrefs.GetInt("Cash") - before} on top of {rewardThisLevel} already credited)");
        }
        yield return new WaitForSecondsRealtime(2f);
        Log($"After button: active scene '{SceneManager.GetActiveScene().name}', timeScale {Time.timeScale}, IsGameRunning {Get(gameState, "IsGameRunning")}");
    }

    readonly Dictionary<int, float> deathGameTime = new Dictionary<int, float>();

}
