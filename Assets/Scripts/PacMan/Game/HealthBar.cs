using UnityEngine;
using UnityEngine.UI; // Required for the Image component
using PacMan.Local;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image _fillImage;

    public float guiHealth = 100f;
    public bool HealthBarFacingCamera = false;
    private float _maxHealth = 100f;
    private float _currentHealth;

    private Transform healthBarTransform;
    private PacManAgentManager _agentManager;

    // A Property to handle health changes safely
    public float CurrentHealth
    {
        get => _currentHealth;
        set
        {
            // Clamp value between 0 and max
            _currentHealth = Mathf.Clamp(value, 0, _maxHealth);
            UpdateUI();
        }
    }
    
    void Awake()
    {
        _currentHealth = _maxHealth;
        _agentManager = GetComponent<PacManAgentManager>();
        healthBarTransform = transform.Find("HealthBar");
        UpdateUI();
    }

    public void SetHealth(float currentHealth, float maxHealth)
    {
        _maxHealth = Mathf.Max(1f, maxHealth);
        CurrentHealth = currentHealth;
    }

    private void UpdateUI()
    {
        if (_fillImage == null)
        {
            return;
        }

        // Fill Amount is a value between 0 and 1
        _fillImage.fillAmount = _currentHealth / Mathf.Max(_maxHealth, 1f);
    }

    void LateUpdate()
    {
        if (_agentManager != null)
        {
            SetHealth(_agentManager.GetHealth(), _agentManager.GetMaxHealth());
        }
        else
        {
            CurrentHealth = guiHealth;
        }

        if (HealthBarFacingCamera && healthBarTransform != null && Camera.main != null)
        {
            healthBarTransform.LookAt(Camera.main.transform.position);
        }
    }
}
