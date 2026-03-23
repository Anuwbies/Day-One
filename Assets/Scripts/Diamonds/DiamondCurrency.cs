using System;
using UnityEngine;

[DisallowMultipleComponent]
public class DiamondCurrency : MonoBehaviour
{
    private const string DefaultPlayerPrefsKey = "PlayerDiamonds";

    public static DiamondCurrency Instance { get; private set; }

    [Header("Save")]
    [SerializeField] private string playerPrefsKey = DefaultPlayerPrefsKey;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool saveImmediately = true;

    [Header("Starting Value")]
    [Min(0)]
    [SerializeField] private int startingDiamonds = 0;

    [Header("Runtime")]
    [SerializeField] private int currentDiamonds;

    public event Action<int> DiamondsChanged;

    public int CurrentDiamonds => currentDiamonds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        LoadDiamonds();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveDiamonds();
        }
    }

    private void OnApplicationQuit()
    {
        SaveDiamonds();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SaveDiamonds();
            Instance = null;
        }
    }

    [ContextMenu("Load Diamonds")]
    public void LoadDiamonds()
    {
        currentDiamonds = PlayerPrefs.GetInt(playerPrefsKey, Mathf.Max(0, startingDiamonds));

        if (!PlayerPrefs.HasKey(playerPrefsKey))
        {
            SaveDiamonds();
        }

        DiamondsChanged?.Invoke(currentDiamonds);
    }

    [ContextMenu("Save Diamonds")]
    public void SaveDiamonds()
    {
        PlayerPrefs.SetInt(playerPrefsKey, currentDiamonds);
        PlayerPrefs.Save();
    }

    public void SetDiamonds(int amount)
    {
        int clampedAmount = Mathf.Max(0, amount);
        if (currentDiamonds == clampedAmount)
        {
            return;
        }

        currentDiamonds = clampedAmount;
        SaveIfNeeded();
        DiamondsChanged?.Invoke(currentDiamonds);
    }

    public void AddDiamonds(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetDiamonds(currentDiamonds + amount);
    }

    public bool TrySpendDiamonds(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (currentDiamonds < amount)
        {
            return false;
        }

        SetDiamonds(currentDiamonds - amount);
        return true;
    }

    public bool HasEnoughDiamonds(int amount)
    {
        return currentDiamonds >= Mathf.Max(0, amount);
    }

    [ContextMenu("Reset Diamonds To Starting Value")]
    public void ResetDiamonds()
    {
        currentDiamonds = Mathf.Max(0, startingDiamonds);
        SaveIfNeeded();
        DiamondsChanged?.Invoke(currentDiamonds);
    }

    private void SaveIfNeeded()
    {
        if (saveImmediately)
        {
            SaveDiamonds();
        }
    }
}
