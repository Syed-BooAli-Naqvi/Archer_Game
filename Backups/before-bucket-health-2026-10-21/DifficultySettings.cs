using UnityEngine;

/// <summary>
/// Difficulty tuning for the level. Lives at Assets/Resources/DifficultySettings.asset.
/// Untick "Enabled" (or the individual toggles) to get the original fixed behaviour back.
/// </summary>
[CreateAssetMenu(fileName = "DifficultySettings", menuName = "ScriptableObjects/DifficultySettings")]
public class DifficultySettings : ScriptableObject
{
    [Tooltip("Master switch. Off = the game plays exactly like before these changes.")]
    public bool enabled = true;

    [Header("Scale enemies with player power")]
    [Tooltip("Player power = fire rate x sqrt(arrows per shot), compared to an un-upgraded player. Extra arrows count less because they spread out.")]
    public bool scaleWithPower = true;

    [Tooltip("Enemies only start getting tougher once player power passes this. Below it, upgrades make the game easier as normal (hard start, beatable after some upgrades).")]
    public float scalingStartsAtPower = 3.5f;

    [Tooltip("Enemy HP multiplier = (power / scalingStartsAtPower) ^ this. 0 = no scaling, 1 = HP grows as fast as your damage.")]
    [Range(0f, 1f)] public float enemyHealthExponent = 0.65f;

    [Tooltip("Wave size multiplier = power ^ this.")]
    [Range(0f, 1f)] public float enemyCountExponent = 0.45f;

    [Tooltip("Boss HP multiplier = power ^ this.")]
    [Range(0f, 1f)] public float bossHealthExponent = 0.5f;

    public int maxEnemiesPerWave = 45;

    [Header("Enemy positioning")]
    [Tooltip("Arrows only fly forward, so enemies are not allowed to walk behind the player; they spread out sideways in front instead.")]
    public bool keepEnemiesInFront = true;

    [Tooltip("How far in front of the player the enemy front line is.")]
    public float frontLineOffset = 1.2f;

    [Header("Spacing (so characters don't overlap)")]
    public bool betterSpacing = true;

    [Tooltip("Minimum distance between enemy centres.")]
    public float enemySpacing = 1.35f;

    [Tooltip("Enemies never come closer than this to the player's centre (still within attack range).")]
    public float playerClearance = 1.8f;

    [Tooltip("Enemies and minions keep this far from the boss's centre.")]
    public float bossClearance = 2.6f;

    [Tooltip("At most this many enemies attack the player at once; the rest wait their turn just behind. 0 = no limit.")]
    public int maxSimultaneousAttackers = 3;

    [Tooltip("The boss charge stops this far short of the marked spot instead of running into the player. A hit is anyone still inside the marker.")]
    public float chargeStopShort = 3f;

    [Header("Visual polish")]
    [Tooltip("Caps particles per effect and thins emission on enemy fire/frost/hit effects (lighter on phones).")]
    public bool particleBudget = true;
    public int maxParticlesPerSystem = 40;
    [Range(0.1f, 1f)] public float particleEmissionScale = 0.5f;

    [Tooltip("When the boss arrives, keep scrolling until the player is off any gate strip before stopping.")]
    public bool stopClearOfGates = true;

    [Tooltip("Keep every enemy inside the left/right edges of the screen, even in big crowds.")]
    public bool keepEnemiesOnScreen = true;
    [Tooltip("Gap kept from the screen edge, as a fraction of screen width (about half an enemy).")]
    [Range(0f, 0.2f)] public float screenEdgeMargin = 0.07f;

    [Tooltip("Spawn waves just above the top of the screen so they walk in instead of popping in under the HUD.")]
    public bool spawnOffscreen = true;

    [Header("Senior's feedback")]
    [Tooltip("Arrows fly until past the top of the screen and fade out, and fade through gates, instead of vanishing.")]
    public bool arrowFade = true;
    public float arrowFadeTime = 0.35f;

    [Tooltip("Arrows only hit within this range; past it they stop doing damage and fade out (so off-screen enemies can't be hit).")]
    public bool limitArrowRange = true;
    [Tooltip("How far (meters) in front of the archer an arrow can still hit.")]
    public float arrowRange = 14f;
    [Tooltip("Also stop arrows at the top edge of the screen, whichever comes first.")]
    public bool arrowRangeStopsAtScreenEdge = true;

    [Tooltip("When the screen is nearly empty, small groups of enemies trickle in so there are no dead spaces.")]
    public bool trickleEnemies = true;
    public int trickleWhenAliveAtMost = 2;
    public float trickleInterval = 2.5f;
    public int trickleGroupMin = 2;
    public int trickleGroupMax = 4;

    [Tooltip("Every so often a bundle of coins appears on the ground; steer into it to collect the whole bundle.")]
    public bool environmentCoins = true;
    public float coinBundleInterval = 8f;
    public int coinsPerBundle = 7;
    [Tooltip("Coins per coin in the bundle (multiplied by the Income upgrade).")]
    public int coinValue = 5;

    [Tooltip("Play a punchy sound on every kill.")]
    public bool killSound = true;

    [Tooltip("Floating damage numbers on every hit.")]
    public bool damageNumbers = true;
    public TMPro.TMP_FontAsset damageNumberFont;

    [Tooltip("When the player picks an ability at a gate, its icon and name pop up, then stay as a badge in the corner.")]
    public bool abilityBanner = true;

    [Header("Game feel")]
    [Tooltip("Frames per second on phones (Unity's default there is 30). 0 = leave Unity's default.")]
    public int targetFrameRate = 60;
    [Tooltip("When the boss dies: explosion, camera shake and slow motion, then the win panel.")]
    public bool bossDeathFinale = true;
    public GameObject bossDeathFx;
    public float winPanelDelay = 1.5f;
    [Tooltip("Red flash at the screen edges when the player is hit, and the lost health drains slowly from the bar.")]
    public bool hitFeedback = true;
    [Tooltip("\"BOSS FIGHT!\" banner, shake and boss music when the boss arrives.")]
    public bool bossIntro = true;
    [Tooltip("The coin counter counts up and pops when coins are added.")]
    public bool coinCounterAnimation = true;

    [Tooltip("Coin icons fly from a collected coin barrel into the coin counter.")]
    public bool coinFlyToCounter = true;
    public GameObject coinBurstFx;
    public GameObject boosterAuraFx;
    public GameObject boosterBurstFx;
    [Tooltip("How long the power-up aura stays on the archer after picking up a booster.")]
    public float boosterAuraSeconds = 4f;

    [Header("Gates & booster")]
    [Tooltip("Small camera shake when arrows hit a gate (throttled into a light rumble).")]
    public bool cameraShakeOnGates = true;
    public float gateShakeDuration = 0.12f;
    public float gateShakeStrength = 0.08f;
    public float gateShakeCooldown = 0.15f;

    [Tooltip("A booster target appears now and then. Shoot it (it has health) to load a 2X button; tapping the button fires extra volleys.")]
    public bool booster = true;
    public float boosterInterval = 20f;
    [Tooltip("Arrow hits needed to make the booster ready to collect.")]
    public int boosterHealth = 10;
    [Tooltip("The booster only takes arrow damage once it is this close (metres ahead of the player).")]
    public float boosterDamageRange = 11f;
    [Tooltip("Arrows per boost volley = current arrow count x this.")]
    public int boosterArrowMultiplier = 2;
    public int boosterVolleys = 2;
    public float boosterVolleyGap = 0.2f;
    public Sprite boosterButtonSprite;

    [Header("UI")]
    [Tooltip("Panels pop in (fade + bounce, buttons one after another), the main menu slides in and the play button pulses.")]
    public bool panelAnimations = true;
    [Tooltip("A 'Welcome, Archer!' popup with how to play.")]
    public bool welcomePanel = true;
    [Tooltip("Off = only on the very first launch. On = once every time the app is opened.")]
    public bool welcomeEveryLaunch = false;
    [Tooltip("Coins given when the player taps the welcome button (only once ever).")]
    public int welcomeCoins = 100;
    public Sprite welcomeFrameSprite;
    public Sprite welcomeButtonSprite;
    [Tooltip("Adds a Vibration on/off switch under Music in the settings panel.")]
    public bool vibrationSetting = true;
    public Sprite vibrationIcon;
    public Sprite switchOnSprite;
    public Sprite switchOffSprite;
    public Sprite switchHandleOnSprite;
    public Sprite switchHandleOffSprite;

    [Header("Level length")]
    [Tooltip("The boss always arrives exactly this many seconds into the run, no matter how upgraded the player is.")]
    public bool fixedLevelLength = true;
    public float levelLengthSeconds = 60f;

    [Header("Wave pacing")]
    [Tooltip("When every enemy is dead, the next wave comes right away instead of waiting for its spawn point to scroll into range.")]
    public bool nextWaveWhenCleared = true;

    [Tooltip("Seconds of calm after a wave is cleared before the next one is pulled in.")]
    public float clearBreather = 1f;

    [Header("Boss: stays in the centre")]
    [Tooltip("The boss only moves along the middle of the road and always turns to look at the player. Its charges land in the middle too, so steer to the side to dodge.")]
    public bool bossStaysCentered = true;

    [Header("Boss: charge attack")]
    [Tooltip("Boss keeps its distance and charges at where the player stands, after a warning. Steer out of the red marker to dodge. Off = original walk-up melee.")]
    public bool bossChargeAttacks = true;
    public float bossHoldDistance = 7f;
    public float chargeInterval = 3f;
    public float chargeTelegraphTime = 1f;
    public float chargeSpeed = 14f;
    public float chargeDamage = 30f;
    public float chargeHitRadius = 1.4f;

    [Header("Boss: summon minions")]
    public bool bossMinions = true;
    public float minionInterval = 12f;
    [Tooltip("Minions per summon (their HP still scales with player power).")]
    public int minionCount = 2;

    // ---------------------------------------------------------------

    static DifficultySettings current;

    public static DifficultySettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<DifficultySettings>("DifficultySettings");
                if (current == null)
                    current = CreateInstance<DifficultySettings>();
            }
            return current;
        }
    }

    public bool ScaleActive => enabled && scaleWithPower;
    public bool NextWaveWhenClearedActive => enabled && nextWaveWhenCleared;
    public bool KeepEnemiesInFrontActive => enabled && keepEnemiesInFront;
    public bool SpacingActive => enabled && betterSpacing;
    public bool FixedLevelLengthActive => enabled && fixedLevelLength;
    public bool ArrowFadeActive => enabled && arrowFade;
    public bool ArrowRangeActive => enabled && limitArrowRange;
    public bool PanelAnimationsActive => enabled && panelAnimations;
    public bool WelcomePanelActive => enabled && welcomePanel;
    public bool VibrationSettingActive => enabled && vibrationSetting;
    public bool TrickleActive => enabled && trickleEnemies;
    public bool CoinsActive => enabled && environmentCoins;
    public bool KillSoundActive => enabled && killSound;
    public bool CameraShakeActive => enabled && cameraShakeOnGates;
    public bool BoosterActive => enabled && booster;
    public bool DamageNumbersActive => enabled && damageNumbers;
    public bool AbilityBannerActive => enabled && abilityBanner;
    public bool BossDeathFinaleActive => enabled && bossDeathFinale;
    public bool HitFeedbackActive => enabled && hitFeedback;
    public bool BossIntroActive => enabled && bossIntro;
    public bool CoinCounterAnimationActive => enabled && coinCounterAnimation;
    public bool ParticleBudgetActive => enabled && particleBudget;
    public bool StopClearOfGatesActive => enabled && stopClearOfGates;
    public bool SpawnOffscreenActive => enabled && spawnOffscreen;
    public bool KeepEnemiesOnScreenActive => enabled && keepEnemiesOnScreen;

    /// <summary>World Z of the top edge of the screen on the ground plane at height y (fallback if no camera).</summary>
    public static float ScreenTopGroundZ(float y, float fallback)
    {
        Camera cam = Camera.main;

        if (cam == null)
            return fallback;

        Ray top = cam.ViewportPointToRay(new Vector3(0.5f, 1f, 0f));
        Plane ground = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        return ground.Raycast(top, out float hit) ? top.GetPoint(hit).z : fallback;
    }

    /// <summary>Caps particle counts and thins emission on every particle system under root (inactive ones too).</summary>
    public static void ApplyParticleBudget(GameObject root)
    {
        DifficultySettings settings = Current;

        if (root == null || !settings.ParticleBudgetActive)
            return;

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.maxParticles = Mathf.Min(main.maxParticles, settings.maxParticlesPerSystem);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTimeMultiplier *= settings.particleEmissionScale;
            emission.rateOverDistanceMultiplier *= settings.particleEmissionScale;

            if (emission.burstCount > 0)
            {
                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);

                for (int i = 0; i < bursts.Length; i++)
                {
                    float count = Mathf.Max(1f, Mathf.Round(bursts[i].count.constantMax * settings.particleEmissionScale));
                    bursts[i].count = new ParticleSystem.MinMaxCurve(count);
                }

                emission.SetBursts(bursts);
            }
        }
    }
    public bool BossCenteredActive => enabled && bossStaysCentered;
    public bool BossChargeActive => enabled && bossChargeAttacks;
    public bool BossMinionsActive => enabled && bossMinions;

    /// <summary>
    /// Damage output relative to an un-upgraded player (1 arrow every 1s), never below 1.
    /// Spread arrows rarely all hit one enemy, so arrow count counts as its square root.
    /// </summary>
    public static float PlayerPower()
    {
        ArcherArrowController shooter = FindAnyObjectByType<ArcherArrowController>();
        if (shooter == null)
            return 1f;

        float shotsPerSecond = 1f / Mathf.Max(shooter.timeBetweenShots, 0.01f);
        return Mathf.Max(1f, shotsPerSecond * Mathf.Sqrt(Mathf.Max(1, shooter.arrowsToShoot)));
    }

    /// <summary>Power above the scaling threshold (1 = at or below it).</summary>
    float ExcessPower(float power) =>
        Mathf.Max(1f, power / Mathf.Max(1f, scalingStartsAtPower));

    public float EnemyHealthMultiplier(float power) =>
        ScaleActive ? Mathf.Pow(ExcessPower(power), enemyHealthExponent) : 1f;

    public float EnemyCountMultiplier(float power) =>
        ScaleActive ? Mathf.Pow(ExcessPower(power), enemyCountExponent) : 1f;

    public float BossHealthMultiplier(float power) =>
        ScaleActive ? Mathf.Pow(ExcessPower(power), bossHealthExponent) : 1f;
}
