using UnityEditor;
using UnityEngine;

/// <summary>
/// Friendly inspector for DifficultySettings: plain-English names, short explanations, foldable sections,
/// sub-settings hidden while their feature is off, and a preview of what the difficulty numbers do.
/// Open it from Tools > Game Settings.
/// </summary>
[CustomEditor(typeof(DifficultySettings))]
public class DifficultySettingsEditor : Editor
{
    [MenuItem("Tools/Game Settings")]
    private static void SelectSettings()
    {
        var settings = Resources.Load<DifficultySettings>("DifficultySettings");
        if (settings != null)
        {
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }

    private GUIStyle sectionStyle, noteStyle;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        sectionStyle ??= new GUIStyle(EditorStyles.foldoutHeader) { fontSize = 13 };
        noteStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.HelpBox(
            "Gameplay extras added on top of the original game. Hover over any setting for a longer explanation.\n" +
            "Changes apply the next time you press Play.", MessageType.Info);

        Toggle("enabled", "Turn ALL extras on", "Off = the game plays exactly like the original version, without any of the systems below.");
        bool all = serializedObject.FindProperty("enabled").boolValue;

        using (new EditorGUI.DisabledScope(!all))
        {
            // ---------------------------------------------------------------- level
            if (Section("level", "Level length"))
            {
                if (Toggle("fixedLevelLength", "Fixed level length", "The boss always arrives at the same moment, no matter how upgraded the player is."))
                    Field("levelLengthSeconds", "Seconds until the boss", "How long the run lasts before the boss appears.");
                End();
            }

            // ---------------------------------------------------------------- difficulty
            if (Section("difficulty", "Difficulty (as you upgrade)"))
            {
                Note("\"Power\" = how strong your shooting is. 1 = no upgrades, about 3.5 = mid upgrades, about 6.7 = high upgrades.");

                if (Toggle("scaleWithPower", "Enemies get tougher as you upgrade", "Keeps the game challenging for heavily upgraded players."))
                {
                    Field("scalingStartsAtPower", "Start getting tougher after power", "Below this, upgrades just make the game easier. Above it, enemies start to grow too.");
                    Field("enemyHealthExponent", "Enemy health growth", "0 = enemies never get tougher. 1 = their health grows as fast as your damage.");
                    Field("enemyCountExponent", "Wave size growth", "0 = waves stay the same size. 1 = waves grow as fast as your damage.");
                    Field("bossHealthExponent", "Boss health growth", "0 = the boss never gets tougher. 1 = its health grows as fast as your damage.");
                    Field("maxEnemiesPerWave", "Max enemies in one wave", "Upper limit so waves can't grow forever.");
                    Preview();
                }

                Field("maxSimultaneousAttackers", "Max enemies attacking at once", "The rest wait their turn just behind. The biggest single knob for how much damage you take. 0 = no limit.");
                End();
            }

            // ---------------------------------------------------------------- enemy flow
            if (Section("flow", "Enemy flow (no empty screens)"))
            {
                if (Toggle("nextWaveWhenCleared", "Next wave comes when you clear the screen", "Instead of waiting for the next wave's spawn point to scroll into view."))
                    Field("clearBreather", "Short pause before it (seconds)", "A moment of calm after clearing a wave.");

                if (Toggle("trickleEnemies", "Fill quiet moments with small groups", "When the screen is almost empty, a few enemies walk in."))
                {
                    Field("trickleWhenAliveAtMost", "...when this many enemies or fewer are left", "");
                    Field("trickleInterval", "Seconds between small groups", "");
                    Field("trickleGroupMin", "Smallest group", "");
                    Field("trickleGroupMax", "Biggest group", "");
                }

                Toggle("spawnOffscreen", "Enemies walk in from off-screen", "So they don't pop in under the pause button or coin counter.");
                End();
            }

            // ---------------------------------------------------------------- positioning
            if (Section("positioning", "Enemy positioning"))
            {
                if (Toggle("keepEnemiesInFront", "Keep enemies in front of the archer", "Arrows only fly forward, so enemies behind you couldn't be hit."))
                    Field("frontLineOffset", "How far in front (metres)", "");

                if (Toggle("betterSpacing", "Stop characters overlapping", "Keeps enemies apart from each other, from the archer and from the boss."))
                {
                    Field("enemySpacing", "Space between enemies (metres)", "");
                    Field("playerClearance", "Space between enemies and archer (metres)", "");
                    Field("bossClearance", "Space around the boss (metres)", "");
                }

                if (Toggle("keepEnemiesOnScreen", "Keep enemies inside the screen", "Big crowds stack into more rows instead of spilling off the sides."))
                    Field("screenEdgeMargin", "Gap from the screen edge", "As a fraction of the screen width (0.07 = 7%).");
                End();
            }

            // ---------------------------------------------------------------- boss
            if (Section("boss", "Boss"))
            {
                Toggle("bossStaysCentered", "Boss stays in the centre and looks at you", "The boss only moves along the middle of the road. Its charges land in the middle too, so steer to the side to dodge.");

                if (Toggle("bossChargeAttacks", "Charge attack (dodge the red circle)", "The boss keeps its distance and charges at where you stand. Off = the original walk-up sword attack."))
                {
                    Field("bossHoldDistance", "Distance the boss keeps from you (metres)", "");
                    Field("chargeInterval", "Seconds between charges", "");
                    Field("chargeTelegraphTime", "Warning time before a charge (seconds)", "How long the red circle shows before the boss charges.");
                    Field("chargeSpeed", "Charge speed", "");
                    Field("chargeDamage", "Charge damage", "You have 100 health.");
                    Field("chargeHitRadius", "Size of the red warning circle (metres)", "You're hit if you're still inside it when the boss lands.");
                    Field("chargeStopShort", "Boss stops this far before you (metres)", "So the boss model doesn't run into the archer.");
                }

                if (Toggle("bossMinions", "Boss summons minions", ""))
                {
                    Field("minionInterval", "Seconds between summons", "");
                    Field("minionCount", "Minions per summon", "");
                }

                Toggle("stopClearOfGates", "Don't stop on a gate when the boss arrives", "The ground keeps scrolling until the archer is off any gate strip.");
                End();
            }

            // ---------------------------------------------------------------- booster
            if (Section("booster", "2X booster barrel"))
            {
                if (Toggle("booster", "Booster barrel on the track", "Shoot it near you until it breaks, then tap the 2X button to fire extra arrows."))
                {
                    Field("boosterInterval", "Seconds between boosters", "");
                    Field("boosterHealth", "Arrow hits needed", "How many arrows must hit it before it breaks.");
                    Field("boosterDamageRange", "Takes hits only within (metres)", "It only starts taking damage once it's this close to you.");
                    Field("boosterArrowMultiplier", "Arrows multiplier when fired", "2 = each boost volley has twice your current arrows.");
                    Field("boosterVolleys", "Number of boost volleys", "");
                    Field("boosterVolleyGap", "Seconds between volleys", "");
                    Field("boosterButtonSprite", "Button image", "");
                }
                End();
            }

            // ---------------------------------------------------------------- coins
            if (Section("coins", "Coin barrels"))
            {
                if (Toggle("environmentCoins", "Coin barrels on the track", "Shoot one until it breaks to collect all its coins."))
                {
                    Field("coinBundleInterval", "Seconds between coin barrels", "");
                    Field("coinsPerBundle", "Coins in each barrel", "");
                    Field("coinValue", "Value of each coin", "Multiplied by the Income upgrade.");
                    Field("coinBucketHealth", "Arrow hits needed", "How many arrows must hit a coin barrel before it breaks. Uses the booster's damage range.");
                }
                End();
            }

            // ---------------------------------------------------------------- effects
            if (Section("ui", "Panels & menus"))
            {
                Toggle("panelAnimations", "Animated panels", "Panels pop in, the main menu slides in and the play button pulses.");

                if (Toggle("welcomePanel", "Welcome panel", "'Welcome, Archer!' popup with how to play."))
                {
                    Field("welcomeEveryLaunch", "Show every time the app opens", "Off = only the first time the game is ever opened.");
                    Field("welcomeCoins", "Starter coins", "Given once, the first time the button is tapped. 0 = no gift.");
                    Field("welcomeFrameSprite", "Window sprite (used when placing)", "Only used when the panel is built into the scene. Edit the placed panel to change it after that.");
                    Field("welcomeButtonSprite", "Button sprite (used when placing)", "");

                    if (GUILayout.Button("Select / place welcome panel in scene"))
                        WelcomePanelPlacer.PlaceFromMenu();

                    if (GUILayout.Button("Show welcome panel again (reset 'seen')"))
                        WelcomePanel.ResetSeen();
                }

                if (Toggle("vibrationSetting", "Vibration switch in settings", "Adds a Vibration on/off row under Music. Off = no switch, vibration stays on."))
                {
                    Field("vibrationIcon", "Icon", "");
                    Field("switchOnSprite", "Switch background (on)", "");
                    Field("switchOffSprite", "Switch background (off)", "");
                    Field("switchHandleOnSprite", "Switch handle (on)", "");
                    Field("switchHandleOffSprite", "Switch handle (off)", "");
                }
                End();
            }

            if (Section("effects", "Effects & sound"))
            {
                if (Toggle("damageNumbers", "Floating damage numbers", ""))
                    Field("damageNumberFont", "Font", "");

                Toggle("abilityBanner", "Ability picked pop-up", "When the player picks an ability at a gate, its icon and name pop up, then stay as a badge in the bottom-left corner.");

                Toggle("coinFlyToCounter", "Coins fly to the coin counter", "When a coin barrel is collected, coin icons arc up into the counter.");
                Field("coinBurstFx", "Coin barrel burst effect", "Particle effect played at the barrel when collected.");
                Field("boosterAuraFx", "Booster aura on archer", "Particle effect around the archer after collecting a booster.");
                Field("boosterAuraSeconds", "Aura duration (seconds)", "");
                Field("boosterBurstFx", "Booster burst effect", "Played when the booster is collected and when the 2X button fires.");

                Toggle("killSound", "Kill sound", "Plays a short sound on every kill.");

                if (Toggle("arrowFade", "Arrows fade out instead of vanishing", "They fly off the screen or through gates and fade."))
                    Field("arrowFadeTime", "Fade time (seconds)", "");

                if (Toggle("limitArrowRange", "Arrows have a damage range", "Past this distance arrows stop hitting and fade out, so enemies outside the view can't be hit."))
                {
                    Field("arrowRange", "Range (meters in front of archer)", "");
                    Field("arrowRangeStopsAtScreenEdge", "Also stop at top edge of screen", "Whichever is closer: the range or the screen edge.");
                }

                if (Toggle("cameraShakeOnGates", "Camera shake when arrows hit gates", ""))
                {
                    Field("gateShakeStrength", "Shake strength", "");
                    Field("gateShakeDuration", "Shake length (seconds)", "");
                    Field("gateShakeCooldown", "Minimum time between shakes (seconds)", "Stops a stream of arrows from shaking constantly.");
                }

                if (Toggle("particleBudget", "Lighter particle effects (better on phones)", "Caps fire, frost and hit effects to fewer particles."))
                {
                    Field("maxParticlesPerSystem", "Max particles per effect", "");
                    Field("particleEmissionScale", "Particle amount", "1 = original amount, 0.5 = half.");
                }
                End();
            }

            if (Section("feel", "Game feel"))
            {
                Field("targetFrameRate", "Frame rate on phones", "Unity's default on phones is 30. 0 = leave the default. Works even with the master switch off.");

                if (Toggle("bossDeathFinale", "Boss death finale", "Explosion, camera shake and slow motion before the win panel."))
                {
                    Field("bossDeathFx", "Explosion effect", "");
                    Field("winPanelDelay", "Win panel delay (seconds)", "");
                }

                Toggle("hitFeedback", "Hit feedback", "Red flash at the screen edges when hit; the lost health drains slowly from the health bar.");
                Toggle("bossIntro", "Boss intro", "\"BOSS FIGHT!\" banner, shake and boss music when the boss arrives.");
                Toggle("coinCounterAnimation", "Coin counter counts up", "The counter counts up and pops when coins are added.");
                End();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ---------------------------------------------------------------- helpers

    private bool Section(string key, string title)
    {
        string prefKey = "DifficultySettingsEditor." + key;
        bool open = SessionState.GetBool(prefKey, key == "level" || key == "difficulty");
        EditorGUILayout.Space(6);
        bool now = EditorGUILayout.BeginFoldoutHeaderGroup(open, title, sectionStyle);
        if (now != open) SessionState.SetBool(prefKey, now);
        if (!now) EditorGUILayout.EndFoldoutHeaderGroup();
        else EditorGUI.indentLevel++;
        return now;
    }

    private void End()
    {
        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private bool Toggle(string property, string label, string tooltip)
    {
        SerializedProperty p = serializedObject.FindProperty(property);
        if (p == null) return false;
        EditorGUILayout.PropertyField(p, new GUIContent(label, tooltip));
        return p.boolValue;
    }

    private void Field(string property, string label, string tooltip)
    {
        SerializedProperty p = serializedObject.FindProperty(property);
        if (p == null) return;
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(p, new GUIContent(label, tooltip));
        EditorGUI.indentLevel--;
    }

    private void Note(string text)
    {
        EditorGUILayout.LabelField(text, noteStyle);
    }

    // Shows what the current numbers mean at no / mid / high upgrades.
    private void Preview()
    {
        serializedObject.ApplyModifiedProperties();
        var s = (DifficultySettings)target;

        (string name, float power)[] levels = { ("No upgrades", 1f), ("Mid upgrades", 3.46f), ("High upgrades", 6.71f) };

        EditorGUILayout.Space(2);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("What this means:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", GUILayout.Width(110));
        EditorGUILayout.LabelField("Enemy health", EditorStyles.miniBoldLabel, GUILayout.Width(85));
        EditorGUILayout.LabelField("Wave size", EditorStyles.miniBoldLabel, GUILayout.Width(70));
        EditorGUILayout.LabelField("Boss health", EditorStyles.miniBoldLabel, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        foreach (var (name, power) in levels)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, GUILayout.Width(110));
            EditorGUILayout.LabelField($"x{s.EnemyHealthMultiplier(power):0.00}", GUILayout.Width(85));
            EditorGUILayout.LabelField($"x{s.EnemyCountMultiplier(power):0.00}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"x{s.BossHealthMultiplier(power):0.00}", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
    }
}
