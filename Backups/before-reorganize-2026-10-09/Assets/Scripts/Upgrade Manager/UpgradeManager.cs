using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    public ArcherArrowController archerArrowController;

    private int cash;

    [Header("Not Enough Cash")]
    [SerializeField] private GameObject notEnoughCashPanel;
    [SerializeField] private float notEnoughCashPanelDuration = 1.5f;

    private Coroutine notEnoughCashCoroutine;

    [Header("Income")]
    [SerializeField] private Button incomeButton;
    [SerializeField] private TMP_Text incomeText;
    [SerializeField] private TMP_Text incomeCostText;
    [SerializeField] private Slider incomeSlider;

    [Header("Fire Rate")]
    [SerializeField] private Button fireRateButton;
    [SerializeField] private TMP_Text fireRateCostText;
    [SerializeField] private TMP_Text fireRateText;
    [SerializeField] private Slider fireRateSlider;

    [Header("Arrow Count")]
    [SerializeField] private Button arrowButton;
    [SerializeField] private TMP_Text arrowText;
    [SerializeField] private TMP_Text arrowCostText;
    [SerializeField] private Slider arrowSlider;

    [Header("Upgrade Settings")]
    [Tooltip("Upgrades needed to increase Income/Fire_Rate by 0.1.")]
    [SerializeField] private int upgradesPerLevel = 10;

    [Tooltip("Number of upgrades needed to increase Arrow Count by 1.")]
    [SerializeField] private int upgradesPerArrow = 5;

    [Header("Starting Upgrade Costs")]
    [SerializeField] private int incomeStartingCost = 100;
    [SerializeField] private int fireRateStartingCost = 100;
    [SerializeField] private int arrowStartingCost = 100;

    [Header("Cost Progression")]
    [Tooltip("Additional cost added after each upgrade.")]
    [SerializeField] private int costIncrease = 50;

    [Tooltip("Additional increase applied to the cost increase after every upgrade.")]
    [SerializeField] private int costIncreaseGrowth = 25;

    [Header("Current Values")]
    [SerializeField] private float income = 1f;
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private int arrowCount = 1;

    [Header("Upgrade Counts")]
    [SerializeField] private int incomeUpgrades;
    [SerializeField] private int fireRateUpgrades;
    [SerializeField] private int arrowUpgrades;

    public float Income => income;
    public float FireRate => fireRate;
    public int ArrowCount => arrowCount;
    public int Cash => cash;

    public GameObject upgradeTut;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!PlayerPrefs.HasKey(SharedPref.Cash))
        {
            PlayerPrefs.SetInt(SharedPref.Cash, 100);
        }

        // Count how many times the game has been started.
        int gameRunCount = PlayerPrefs.GetInt("GameRunCount", 0);
        gameRunCount++;
        PlayerPrefs.SetInt("GameRunCount", gameRunCount);
        PlayerPrefs.Save();

        upgradesPerLevel = Mathf.Max(1, upgradesPerLevel);
        upgradesPerArrow = Mathf.Max(1, upgradesPerArrow);

        incomeStartingCost = Mathf.Max(1, incomeStartingCost);
        fireRateStartingCost = Mathf.Max(1, fireRateStartingCost);
        arrowStartingCost = Mathf.Max(1, arrowStartingCost);

        costIncrease = Mathf.Max(0, costIncrease);
        costIncreaseGrowth = Mathf.Max(0, costIncreaseGrowth);

        Load();
        UpdateValues();

        if (notEnoughCashPanel)
            notEnoughCashPanel.SetActive(false);

        SetupUpgradeTutorial(gameRunCount);
    }

    private void SetupUpgradeTutorial(int gameRunCount)
    {
        if (!upgradeTut)
            return;

        bool upgradeAlreadyDone =
            PlayerPrefs.GetInt(SharedPref.UpgradeDoneOnce, 0) == 1;

        // Show tutorial only on the 2nd game run,
        // and only if the player has never upgraded.
        if (gameRunCount == 2 && !upgradeAlreadyDone)
        {
            upgradeTut.SetActive(true);
        }
        else
        {
            upgradeTut.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (incomeButton)
            incomeButton.onClick.AddListener(UpgradeIncome);

        if (fireRateButton)
            fireRateButton.onClick.AddListener(UpgradeFireRate);

        if (arrowButton)
            arrowButton.onClick.AddListener(UpgradeArrowCount);
    }

    private void OnDisable()
    {
        if (incomeButton)
            incomeButton.onClick.RemoveListener(UpgradeIncome);

        if (fireRateButton)
            fireRateButton.onClick.RemoveListener(UpgradeFireRate);

        if (arrowButton)
            arrowButton.onClick.RemoveListener(UpgradeArrowCount);
    }

    private void Start()
    {
        SetupSlider(incomeSlider);
        SetupSlider(fireRateSlider);
        SetupSlider(arrowSlider);

        RefreshUI();
    }

    private void SetupSlider(Slider slider)
    {
        if (!slider)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;
    }

    // =========================================================
    // UPGRADE
    // =========================================================

    public void UpgradeIncome()
    {
        int cost = GetIncomeCost();

        if (!CanAfford(cost))
        {
            ShowNotEnoughCash();
            return;
        }

        SoundManager.Instance.PlaySound(SoundName.Upgrade);
        PlayerPrefs.SetInt(SharedPref.UpgradeDoneOnce, 1);
        DisableTut();
        cash -= cost;
        incomeUpgrades++;

        UpdateValues();
        Save();
        RefreshUI();
    }

    public void UpgradeFireRate()
    {
        int cost = GetFireRateCost();

        if (!CanAfford(cost))
        {
            ShowNotEnoughCash();
            return;
        }

        SoundManager.Instance.PlaySound(SoundName.Upgrade);
        PlayerPrefs.SetInt(SharedPref.UpgradeDoneOnce, 1);
        DisableTut();

        cash -= cost;
        fireRateUpgrades++;

        UpdateValues();
        Save();
        RefreshUI();
    }

    public void UpgradeArrowCount()
    {
        int cost = GetArrowCost();

        if (!CanAfford(cost))
        {
            ShowNotEnoughCash();
            return;
        }

        SoundManager.Instance.PlaySound(SoundName.Upgrade);
        PlayerPrefs.SetInt(SharedPref.UpgradeDoneOnce, 1);
        DisableTut();
        cash -= cost;
        arrowUpgrades++;

        UpdateValues();
        Save();
        RefreshUI();
    }

    // =========================================================
    // NOT ENOUGH CASH
    // =========================================================

    private void ShowNotEnoughCash()
    {
        if (!notEnoughCashPanel)
            return;

        notEnoughCashPanel.SetActive(true);
    }

    // =========================================================
    // VALUES
    // =========================================================

    private void UpdateValues()
    {
        income = 1f + (float)incomeUpgrades / upgradesPerLevel;

        fireRate = 1f + (float)fireRateUpgrades / upgradesPerLevel;

        arrowCount = 1 + arrowUpgrades / upgradesPerArrow;

        if (archerArrowController)
        {
            archerArrowController.arrowsToShoot = arrowCount;

            // Higher Fire Rate = lower time between shots.
            archerArrowController.timeBetweenShots =
                Mathf.Max(1f / fireRate, 0.15f);
        }
    }

    // =========================================================
    // COST
    // =========================================================

    private int GetIncomeCost()
    {
        return CalculateCost(incomeStartingCost, incomeUpgrades);
    }

    private int GetFireRateCost()
    {
        return CalculateCost(fireRateStartingCost, fireRateUpgrades);
    }

    private int GetArrowCost()
    {
        return CalculateCost(arrowStartingCost, arrowUpgrades);
    }

    private int CalculateCost(int startingCost, int upgradeCount)
    {
        if (upgradeCount <= 0)
            return startingCost;

        int cost = startingCost;

        for (int i = 0; i < upgradeCount; i++)
        {
            cost += costIncrease + (i * costIncreaseGrowth);

            if (cost >= int.MaxValue)
                return int.MaxValue;
        }

        return Mathf.Max(1, cost);
    }

    // =========================================================
    // CASH
    // =========================================================

    private bool CanAfford(int cost)
    {
        return cash >= cost;
    }

    public void AddCash(int amount)
    {
        if (amount <= 0)
            return;

        cash += amount;

        Save();
        RefreshUI();
    }

    public bool SpendCash(int amount)
    {
        if (!CanAfford(amount))
            return false;

        cash -= amount;

        Save();
        RefreshUI();

        return true;
    }

    // =========================================================
    // UI
    // =========================================================

    private void RefreshUI()
    {
        if (incomeText)
            incomeText.text = income.ToString("F1");

        if (incomeCostText)
            incomeCostText.text = GetIncomeCost().ToString();

        if (incomeSlider)
        {
            incomeSlider.value =
                (float)(incomeUpgrades % upgradesPerLevel)
                / upgradesPerLevel;
        }

        if (fireRateText)
            fireRateText.text = fireRate.ToString("F1");

        if (fireRateCostText)
            fireRateCostText.text = GetFireRateCost().ToString();

        if (fireRateSlider)
        {
            fireRateSlider.value =
                (float)(fireRateUpgrades % upgradesPerLevel)
                / upgradesPerLevel;
        }

        if (arrowText)
            arrowText.text = arrowCount.ToString();

        if (arrowCostText)
            arrowCostText.text = GetArrowCost().ToString();

        if (arrowSlider)
        {
            arrowSlider.value =
                (float)(arrowUpgrades % upgradesPerArrow)
                / upgradesPerArrow;
        }

        // if (incomeButton)
        //     incomeButton.interactable = CanAfford(GetIncomeCost());

        // if (fireRateButton)
        //     fireRateButton.interactable = CanAfford(GetFireRateCost());

        // if (arrowButton)
        //     arrowButton.interactable = CanAfford(GetArrowCost());
    }

    // =========================================================
    // SAVE / LOAD
    // =========================================================

    private void Save()
    {
        PlayerPrefs.SetInt(SharedPref.IncomeKey, incomeUpgrades);
        PlayerPrefs.SetInt(SharedPref.FireRateKey, fireRateUpgrades);
        PlayerPrefs.SetInt(SharedPref.ArrowKey, arrowUpgrades);
        PlayerPrefs.SetInt(SharedPref.Cash, cash);

        PlayerPrefs.Save();
    }

    private void Load()
    {
        incomeUpgrades = PlayerPrefs.GetInt(
            SharedPref.IncomeKey,
            0
        );

        fireRateUpgrades = PlayerPrefs.GetInt(
            SharedPref.FireRateKey,
            0
        );

        arrowUpgrades = PlayerPrefs.GetInt(
            SharedPref.ArrowKey,
            0
        );

        cash = PlayerPrefs.GetInt(
            SharedPref.Cash
        );
    }

    public void DisableTut()
    {
        if (PlayerPrefs.GetInt(SharedPref.UpgradeDoneOnce) == 1)
        {
            upgradeTut.SetActive(false);
        }
    }
}