using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class ScoreManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text scoreText;
    public Slider progressSlider;
    public bool sliderUseNormalized = true;

    [Header("Slider Smooth")]
    public bool smoothSlider = true;
    public float sliderSmoothSpeed = 8f;

    [Header("Level Label")]
    public TMP_Text levelText;
    public string levelPrefix = "Level";
    public string levelSuffix = "Completed";
    public int levelNumberOffset = 1;

    [Header("Win")]
    public int targetScore = 50;
    public GameObject winObject;

    [Header("Fail")]
    public GameObject failedObject; // fail panel

    [Header("Win FX & Anim")]
    public ParticleSystem winParticles;
    public float particlesDelay = 0.4f;
    public float fadeDuration = 0.35f;
    public float scaleDuration = 0.35f;
    public float startScale = 0.7f;

    [Header("Fail Anim")]
    public float failFadeDuration = 0.35f;
    public float failScaleDuration = 0.35f;
    public float failStartScale = 0.7f;

    public int Score { get; private set; }
    public bool HasWon { get; private set; }
    public bool HasFailed { get; private set; }

    CanvasGroup winCanvasGroup;
    CanvasGroup failCanvasGroup;
    Coroutine winRoutine;
    Coroutine failRoutine;

    float sliderTargetValue;

    void Start()
    {
        // Win panel init
        if (winObject)
        {
            winCanvasGroup = winObject.GetComponent<CanvasGroup>();
            if (!winCanvasGroup) winCanvasGroup = winObject.AddComponent<CanvasGroup>();

            winCanvasGroup.alpha = 0f;
            winObject.transform.localScale = Vector3.one * startScale;
            winObject.SetActive(false);
        }

        // Fail panel init
        if (failedObject)
        {
            failCanvasGroup = failedObject.GetComponent<CanvasGroup>();
            if (!failCanvasGroup) failCanvasGroup = failedObject.AddComponent<CanvasGroup>();

            failCanvasGroup.alpha = 0f;
            failedObject.transform.localScale = Vector3.one * failStartScale;
            failedObject.SetActive(false);
        }

        UpdateLevelText();
        SetupSlider();
        RefreshUI(immediateSlider: true);
    }

    void Update()
    {
        if (progressSlider && smoothSlider)
        {
            progressSlider.value = Mathf.Lerp(
                progressSlider.value,
                sliderTargetValue,
                Time.unscaledDeltaTime * sliderSmoothSpeed
            );
        }
    }

    void SetupSlider()
    {
        if (!progressSlider) return;

        if (sliderUseNormalized)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
        }
        else
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = targetScore;
        }
    }

    void UpdateLevelText()
    {
        if (!levelText) return;

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        int levelNumber = buildIndex + levelNumberOffset; // 0->1, 1->2...

        levelText.text = $"{levelPrefix} {levelNumber} {levelSuffix}";
    }

    public void AddScore(int amount)
    {
        if (HasWon || HasFailed) return;
        if (amount <= 0) return;

        Score += amount;
        RefreshUI(immediateSlider: !smoothSlider);

        if (Score >= targetScore)
        {
            HasWon = true;
            ForceSliderFull();

            // fail coroutine varsa durdur
            if (failRoutine != null) StopCoroutine(failRoutine);
            failRoutine = null;

            if (winRoutine != null) StopCoroutine(winRoutine);
            winRoutine = StartCoroutine(WinSequence());
        }
    }

    public void ShowFailed()
    {
        if (HasWon || HasFailed) return;
        HasFailed = true;

        // win coroutine varsa durdur + win'i kapat
        if (winRoutine != null) StopCoroutine(winRoutine);
        winRoutine = null;

        if (winObject) winObject.SetActive(false);

        if (!failedObject) return;

        if (failRoutine != null) StopCoroutine(failRoutine);
        failRoutine = StartCoroutine(FailSequence());
    }

    void ForceSliderFull()
    {
        if (!progressSlider) return;

        if (sliderUseNormalized)
        {
            sliderTargetValue = 1f;
            if (!smoothSlider) progressSlider.value = 1f;
        }
        else
        {
            progressSlider.maxValue = targetScore;
            sliderTargetValue = targetScore;
            if (!smoothSlider) progressSlider.value = targetScore;
        }
    }

    IEnumerator WinSequence()
    {
        // Particle önce
        if (winParticles)
        {
            winParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            winParticles.Play();
        }

        if (particlesDelay > 0f)
            yield return new WaitForSecondsRealtime(particlesDelay);

        if (!winObject) yield break;

        winObject.SetActive(true);

        if (!winCanvasGroup) winCanvasGroup = winObject.GetComponent<CanvasGroup>();
        if (!winCanvasGroup) winCanvasGroup = winObject.AddComponent<CanvasGroup>();

        winCanvasGroup.alpha = 0f;
        winObject.transform.localScale = Vector3.one * startScale;

        float fadeT = 0f;
        float scaleT = 0f;

        while (fadeT < fadeDuration || scaleT < scaleDuration)
        {
            if (fadeT < fadeDuration)
            {
                fadeT += Time.unscaledDeltaTime;
                winCanvasGroup.alpha = Mathf.Clamp01(fadeT / fadeDuration);
            }

            if (scaleT < scaleDuration)
            {
                scaleT += Time.unscaledDeltaTime;
                float s = Mathf.Clamp01(scaleT / scaleDuration);
                float eased = EaseOutBack(s);
                float currentScale = Mathf.Lerp(startScale, 1f, eased);
                winObject.transform.localScale = Vector3.one * currentScale;
            }

            yield return null;
        }

        winCanvasGroup.alpha = 1f;
        winObject.transform.localScale = Vector3.one;
    }

    IEnumerator FailSequence()
    {
        if (!failedObject) yield break;

        failedObject.SetActive(true);

        if (!failCanvasGroup) failCanvasGroup = failedObject.GetComponent<CanvasGroup>();
        if (!failCanvasGroup) failCanvasGroup = failedObject.AddComponent<CanvasGroup>();

        failCanvasGroup.alpha = 0f;
        failedObject.transform.localScale = Vector3.one * failStartScale;

        float fadeT = 0f;
        float scaleT = 0f;

        while (fadeT < failFadeDuration || scaleT < failScaleDuration)
        {
            if (fadeT < failFadeDuration)
            {
                fadeT += Time.unscaledDeltaTime;
                failCanvasGroup.alpha = Mathf.Clamp01(fadeT / failFadeDuration);
            }

            if (scaleT < failScaleDuration)
            {
                scaleT += Time.unscaledDeltaTime;
                float s = Mathf.Clamp01(scaleT / failScaleDuration);
                float eased = EaseOutBack(s);
                float currentScale = Mathf.Lerp(failStartScale, 1f, eased);
                failedObject.transform.localScale = Vector3.one * currentScale;
            }

            yield return null;
        }

        failCanvasGroup.alpha = 1f;
        failedObject.transform.localScale = Vector3.one;
    }

    float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    public void ResetScore()
    {
        Score = 0;
        HasWon = false;
        HasFailed = false;

        if (winRoutine != null) StopCoroutine(winRoutine);
        winRoutine = null;

        if (failRoutine != null) StopCoroutine(failRoutine);
        failRoutine = null;

        if (winParticles)
            winParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (winObject)
        {
            if (!winCanvasGroup) winCanvasGroup = winObject.GetComponent<CanvasGroup>();
            if (!winCanvasGroup) winCanvasGroup = winObject.AddComponent<CanvasGroup>();

            winCanvasGroup.alpha = 0f;
            winObject.transform.localScale = Vector3.one * startScale;
            winObject.SetActive(false);
        }

        if (failedObject)
        {
            if (!failCanvasGroup) failCanvasGroup = failedObject.GetComponent<CanvasGroup>();
            if (!failCanvasGroup) failCanvasGroup = failedObject.AddComponent<CanvasGroup>();

            failCanvasGroup.alpha = 0f;
            failedObject.transform.localScale = Vector3.one * failStartScale;
            failedObject.SetActive(false);
        }

        SetupSlider();
        RefreshUI(immediateSlider: true);
    }

    void RefreshUI(bool immediateSlider)
    {
        if (scoreText)
            scoreText.text = $"Score: {Score}/{targetScore}";

        if (!progressSlider) return;

        if (sliderUseNormalized)
        {
            float t = (targetScore <= 0) ? 1f : Mathf.Clamp01((float)Score / targetScore);
            sliderTargetValue = t;
            if (immediateSlider) progressSlider.value = t;
        }
        else
        {
            progressSlider.maxValue = targetScore;
            float v = Mathf.Clamp(Score, 0, targetScore);
            sliderTargetValue = v;
            if (immediateSlider) progressSlider.value = v;
        }
    }
}