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

// Plays the game like a real player, from a fresh save, at normal speed with normal health:
// play a run -> on death press Continue / on win press Claim -> buy upgrades -> play again,
// until the boss is defeated (or the time budget runs out). Records stats per run.
public class PlayerCareerTest
{
    const float BudgetRealSeconds = 55f * 60f;
    const int MaxRuns = 100;
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    static readonly string[] PrefKeys =
    {
        "Cash", "Upgrade_Income", "Upgrade_Fire_Rate", "Upgrade_Arrow", "UpgradeDoneOnce", "GameRunCount", "MusicLevel", "SoundLevel"
    };

    StringBuilder report;
    List<string> errors;
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
        return type.GetProperty(member, All)?.GetValue(target);
    }

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
        Debug.Log("[CAREER] " + line);
        File.WriteAllText(Path.Combine(outDir, "career_report.txt"), report.ToString());
    }

    void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add($"{type}: {condition}".Split('\n')[0]);
    }

    void CaptureUI(string label)
    {
        try
        {
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(outDir, $"career_{++shotIndex:00}_{label}.png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
        catch (Exception) { }
    }

    [UnityTest, Timeout(3_600_000)]
    public IEnumerator PlayLikeAPlayer()
    {
        report = new StringBuilder();
        errors = new List<string>();
        outDir = Path.Combine(Application.dataPath, "../Temp/PlayTest");
        Directory.CreateDirectory(outDir);
        foreach (var f in Directory.GetFiles(outDir, "career_*")) File.Delete(f);

        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceived += OnLog;

        var backup = new Dictionary<string, (bool has, int i, float f)>();
        foreach (string k in PrefKeys)
            backup[k] = (PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k), PlayerPrefs.GetFloat(k));

        // Fresh save, like a new player.
        foreach (string k in PrefKeys) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();

        try
        {
            IEnumerator run = Career();
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
            foreach (var g in errors.GroupBy(e => e).Take(10)) Log($"  {g.Count()}x {g.Key}");
            Log("(Your real save was restored.)");
        }
    }

    IEnumerator Boot()
    {
        Type gameState = T("ArcherGameState");
        Call(gameState, "StopGame");
        foreach (string persistent in new[] { "LoadingScript", "SoundManager" })
            foreach (var c in UnityEngine.Object.FindObjectsByType(T(persistent), FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>())
                UnityEngine.Object.Destroy(c.gameObject);
        Time.timeScale = 1f;
        yield return null;

        SceneManager.LoadScene("My Archery Loading");
        float r0 = Time.realtimeSinceStartup;
        while (SceneManager.GetActiveScene().name != "My Archery Loading" && Time.realtimeSinceStartup - r0 < 10f) yield return null;
        while (SceneManager.GetActiveScene().name != "My Archery Gameplay" && Time.realtimeSinceStartup - r0 < 30f) yield return null;
        Assert.AreEqual("My Archery Gameplay", SceneManager.GetActiveScene().name, "Gameplay scene never loaded");
        yield return new WaitForSecondsRealtime(1f);
    }

    IEnumerator Career()
    {
        Type gameState = T("ArcherGameState");
        Type uiType = T("MainMenuUIHandler");
        Type upgradeT = T("UpgradeManager");
        Type enemyHealthT = T("EnemyHealth");
        Type bossHealthT = T("BossHealth");
        Type bossAIT = T("BossAI");
        Type playerHealthT = T("ArcherPlayerHealth");
        Type arrowCtrlT = T("ArcherArrowController");
        Type spawnerT = T("EnemySpawner");

        float careerStart = Time.realtimeSinceStartup;
        Log("Playing like a new player: fresh save, normal speed, 100 HP, Continue after a loss, Claim (no ad) after a win,");
        Log("then spend coins on the cheapest upgrade until broke. Steering goes through the game's own touch-movement code.");
        Log("");
        Log("Run | Arrows FireRate Income | Result        | Time   | Wave | Kills | Coins earned | Coins after | Bought");

        yield return Boot();

        bool won = false;
        int runNo = 0;
        var totalBought = new Dictionary<string, int> { { "Arrow", 0 }, { "FireRate", 0 }, { "Income", 0 } };

        while (!won && runNo < MaxRuns && Time.realtimeSinceStartup - careerStart < BudgetRealSeconds)
        {
            runNo++;
            // Wait for the menu after a scene reload.
            float w0 = Time.realtimeSinceStartup;
            while ((Get(uiType, "Instance") == null || Get(upgradeT, "Instance") == null) && Time.realtimeSinceStartup - w0 < 20f) yield return null;
            yield return new WaitForSecondsRealtime(1f);

            object ui = Get(uiType, "Instance");
            object upg = Get(upgradeT, "Instance");
            string loadout = $"{Get(upg, "ArrowCount"),6} {Get(upg, "FireRate"),8:0.0} {Get(upg, "Income"),6:0.0}";
            if (runNo == 1) { yield return new WaitForEndOfFrame(); CaptureUI("menu_first_launch"); }

            Call(ui, "TapToStart");
            w0 = Time.realtimeSinceStartup;
            while (!(bool)Get(gameState, "IsGameRunning") && Time.realtimeSinceStartup - w0 < 10f) yield return null;
            yield return null;

            var arrowCtrl = FindAll(arrowCtrlT).First();
            Transform player = (Transform)Get(arrowCtrl, "player");
            float minX = (float)Get(arrowCtrl, "minPlayerX"), maxX = (float)Get(arrowCtrl, "maxPlayerX");
            var playerHealth = FindAll(playerHealthT).First();
            var spawners = FindAll(spawnerT).Where(s => !(bool)Get(s, "isBoss")).ToList();
            GameObject completePanel = (GameObject)Get(ui, "completePanel");
            GameObject failedPanel = (GameObject)Get(ui, "failedPanel");

            float g0 = Time.time;
            var killed = new HashSet<int>();
            Component boss = null;
            float bossMax = 0f, telegraphStart = -1f;
            string result = "?";
            bool shotMid = false;

            while (Time.realtimeSinceStartup - w0 < 400f)
            {
                float t = Time.time - g0;

                foreach (var e in FindAll(enemyHealthT))
                    if ((bool)Get(e, "isDead")) killed.Add(e.GetInstanceID());
                var alive = FindAll(enemyHealthT).Where(e => !(bool)Get(e, "isDead")).ToList();

                if (boss == null) { boss = FindAll(bossHealthT).FirstOrDefault(); if (boss != null) bossMax = (float)Get(boss, "maxHealth"); }
                bool bossAlive = boss != null && boss && boss.gameObject.activeInHierarchy && !(bool)Call(boss, "IsDead");

                // --- steering, through the game's own touch handler (same speed limits as a finger) ---
                Component bossAI = bossAlive ? boss.GetComponent(bossAIT) : null;
                bool telegraphing = bossAI != null && (bool)Get(bossAI, "IsTelegraphing");
                bool charging = bossAI != null && Get(bossAI, "chargeState")?.ToString() == "Charging";
                if (telegraphing && telegraphStart < 0) telegraphStart = t;
                if (!telegraphing) telegraphStart = -1f;

                Transform threat = bossAlive ? boss.transform
                    : alive.OrderBy(a => (a.transform.position - player.position).sqrMagnitude).FirstOrDefault()?.transform;
                float targetX = threat != null
                    ? Mathf.Clamp(threat.position.x, minX, maxX)
                    : Mathf.Lerp(minX, maxX, 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / 7f));
                if ((telegraphing && t - telegraphStart >= 0.35f) || charging)
                {
                    float markX = ((Vector3)Get(bossAI, "ChargeTarget")).x;
                    targetX = Mathf.Clamp(markX > (minX + maxX) * 0.5f ? markX - 3f : markX + 3f, minX, maxX);
                }
                if ((bool)Get(gameState, "IsGameRunning") && Time.timeScale > 0f)
                {
                    float screenX = Mathf.InverseLerp(minX, maxX, targetX) * Screen.width;
                    Call(arrowCtrl, "MovePlayer", new Vector2(screenX, Screen.height * 0.3f));
                }

                if (!shotMid && runNo % 5 == 1 && t > 15f) { shotMid = true; yield return new WaitForEndOfFrame(); CaptureUI($"run{runNo}_t15"); }

                if (completePanel && completePanel.activeInHierarchy) { result = "WON"; break; }
                if (failedPanel && failedPanel.activeInHierarchy)
                {
                    result = bossAlive ? $"died at boss ({(float)Get(boss, "currentHealth") / bossMax * 100f:F0}% HP left)" : "died";
                    break;
                }
                yield return null;
            }

            float runTime = Time.time - g0;
            int wave = spawners.Count(s => (bool)Get(s, "hasTriggered"));
            int earned = (int)Get(ui, "rewardAmountThisLevel");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForEndOfFrame();
            if (result == "WON" || runNo == 1) CaptureUI(result == "WON" ? $"run{runNo}_win" : $"run{runNo}_death");

            if (result == "WON") { won = true; Call(ui, "Claim"); }
            else Call(ui, "Continue");

            // Back at the menu: spend coins like a typical player (cheapest upgrade first).
            w0 = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(0.5f);
            while ((Get(upgradeT, "Instance") == null || !(bool)(Get(upgradeT, "Instance") as Component)) && Time.realtimeSinceStartup - w0 < 20f) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            upg = Get(upgradeT, "Instance");
            var bought = new List<string>();
            while (true)
            {
                int cash = (int)Get(upg, "Cash");
                var options = new (string name, int cost, string method)[]
                {
                    ("Arrow", (int)Call(upg, "GetArrowCost"), "UpgradeArrowCount"),
                    ("FireRate", (int)Call(upg, "GetFireRateCost"), "UpgradeFireRate"),
                    ("Income", (int)Call(upg, "GetIncomeCost"), "UpgradeIncome"),
                };
                var pick = options.OrderBy(o => o.cost).First();
                if (pick.cost > cash) break;
                Call(upg, pick.method);
                bought.Add(pick.name);
                totalBought[pick.name]++;
                yield return null;
            }
            string boughtText = bought.Count == 0 ? "-" : string.Join(" ", bought.GroupBy(b => b).Select(g => $"{g.Key}x{g.Count()}"));

            Log($"{runNo,3} | {loadout} | {result,-13} | {runTime,5:F0}s | {wave,2}/{spawners.Count} | {killed.Count,5} | {earned,12} | {(int)Get(upg, "Cash"),11} | {boughtText}");
        }

        float total = Time.realtimeSinceStartup - careerStart;
        Log("");
        Log(won ? $"=== BOSS DEFEATED on run {runNo}, after {total / 60f:F1} minutes of real play (incl. loading/menus) ==="
                : $"=== Boss not defeated after {runNo} runs / {total / 60f:F1} minutes ===");
        Log($"Upgrades bought in total: arrows {totalBought["Arrow"]}, fire rate {totalBought["FireRate"]}, income {totalBought["Income"]}");
    }
}
