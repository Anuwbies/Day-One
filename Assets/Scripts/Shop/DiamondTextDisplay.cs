using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class DiamondTextDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text diamondText;

    [Header("Formatting")]
    [SerializeField] private string prefix = "";
    [SerializeField] private string suffix = "";
    [SerializeField] private bool useThousandsSeparator;

    private DiamondCurrency boundCurrency;

    private void Reset()
    {
        diamondText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        BindToCurrency();
        RefreshDisplay();
    }

    private void Start()
    {
        BindToCurrency();
        RefreshDisplay();
    }

    private void LateUpdate()
    {
        if (boundCurrency == null && DiamondCurrency.Instance != null)
        {
            BindToCurrency();
            RefreshDisplay();
        }
    }

    private void OnDisable()
    {
        UnbindFromCurrency();
    }

    private void OnDestroy()
    {
        UnbindFromCurrency();
    }

    [ContextMenu("Refresh Display")]
    public void RefreshDisplay()
    {
        if (diamondText == null)
        {
            diamondText = GetComponent<TMP_Text>();
        }

        if (diamondText == null)
        {
            return;
        }

        int diamondAmount = boundCurrency != null ? boundCurrency.CurrentDiamonds : 0;
        diamondText.text = FormatDiamondAmount(diamondAmount);
    }

    private void BindToCurrency()
    {
        DiamondCurrency currentCurrency = DiamondCurrency.Instance;
        if (boundCurrency == currentCurrency)
        {
            return;
        }

        UnbindFromCurrency();

        if (currentCurrency == null)
        {
            return;
        }

        boundCurrency = currentCurrency;
        boundCurrency.DiamondsChanged += HandleDiamondsChanged;
    }

    private void UnbindFromCurrency()
    {
        if (boundCurrency == null)
        {
            return;
        }

        boundCurrency.DiamondsChanged -= HandleDiamondsChanged;
        boundCurrency = null;
    }

    private void HandleDiamondsChanged(int diamondAmount)
    {
        if (diamondText == null)
        {
            diamondText = GetComponent<TMP_Text>();
        }

        if (diamondText == null)
        {
            return;
        }

        diamondText.text = FormatDiamondAmount(diamondAmount);
    }

    private string FormatDiamondAmount(int diamondAmount)
    {
        string formattedAmount = useThousandsSeparator ? diamondAmount.ToString("N0") : diamondAmount.ToString();
        return prefix + formattedAmount + suffix;
    }
}
