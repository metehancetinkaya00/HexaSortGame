using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Renderer))]
public class Tower : MonoBehaviour
{
    [Header("Elements")]
    [SerializeField] private Animator animator;
    private Renderer _renderer;

    [Header("Settings")]
    [SerializeField] private int totalStepCount = 550;
    [SerializeField] private float maxFillPercent = 1f;

    [Header("Debug")]
    [SerializeField] private int currentStep = 0;

    private float fillPercent = 0f;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            Fill();
    }

    private void Fill()
    {
        if (currentStep >= totalStepCount)
            return;

        currentStep++;
        fillPercent = (float)currentStep / totalStepCount;
        fillPercent = Mathf.Clamp01(fillPercent);

        UpdateMaterials();

        if (animator != null)
            animator.Play("Tower");

     
    }

    private void UpdateMaterials()
    {
        foreach (Material material in _renderer.materials)
        {
            material.SetFloat("_Fill_Percent", fillPercent * maxFillPercent);
        }
    }
}