using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Tower : MonoBehaviour
{
    [Header("Elements")]
    [SerializeField] private Animator animator;

    private Renderer cachedRenderer;

    [Header("Settings")]
    [SerializeField] private int totalStepCount = 550;
    [SerializeField] private float maxFillPercent = 1f;
    [SerializeField] private float holdFillInterval = 0.02f;
    [SerializeField] private string shaderFillPropertyName = "_Fill_Percent";

    [Header("Debug")]
    [SerializeField] private int currentStep = 0;

    private float fillPercent = 0f;
    private bool isHoldingFillButton = false;
    private float fillTimer = 0f;

    private void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        LoadCurrentFillFromProgress();
        UpdateMaterials();
    }

    private void Update()
    {
        if (!isHoldingFillButton)
        {
            fillTimer = 0f;
            return;
        }

        if (!TowerProgress.HasPendingStep())
        {
            fillTimer = 0f;
            return;
        }

        if (currentStep >= totalStepCount)
        {
            fillTimer = 0f;
            return;
        }

        fillTimer += Time.deltaTime;

        while (fillTimer >= holdFillInterval)
        {
            fillTimer -= holdFillInterval;

            bool consumed = TowerProgress.ConsumeOnePendingStep();
            if (!consumed)
            {
                fillTimer = 0f;
                break;
            }

            FillOneStep();

            if (currentStep >= totalStepCount)
            {
                fillTimer = 0f;
                break;
            }
        }
    }

    public void StartHoldFill()
    {
        isHoldingFillButton = true;
    }

    public void StopHoldFill()
    {
        isHoldingFillButton = false;
        fillTimer = 0f;
    }

    public void LoadCurrentFillFromProgress()
    {
        currentStep = Mathf.Clamp(TowerProgress.GetAppliedStepCount(), 0, totalStepCount);
        UpdateFillPercent();
    }

    private void FillOneStep()
    {
        if (currentStep >= totalStepCount)
        {
            return;
        }

        currentStep++;
        UpdateFillPercent();
        UpdateMaterials();

        if (animator != null)
        {
            animator.Play("Tower", 0, 0f);
        }
    }

    private void UpdateFillPercent()
    {
        fillPercent = (float)currentStep / totalStepCount;
        fillPercent = Mathf.Clamp01(fillPercent);
    }

    private void UpdateMaterials()
    {
        if (cachedRenderer == null)
        {
            return;
        }

        Material[] materials = cachedRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i].SetFloat(shaderFillPropertyName, fillPercent * maxFillPercent);
        }
    }
}