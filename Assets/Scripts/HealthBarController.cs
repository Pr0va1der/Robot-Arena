using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarController : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerHP playerHP;
    public Image hpFill;
    public TextMeshProUGUI hpText;

    void Start()
    {
        // Подписываемся на изменение хп
        playerHP.OnHealthChanged += UpdateHealthBar;
    }

    void OnDestroy()
    {
        playerHP.OnHealthChanged -= UpdateHealthBar;
    }

    void UpdateHealthBar(float current, float max)
    {
        hpFill.fillAmount = current / max;

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(current)}";
    }
}


