using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    public GameObject failedObject;

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

    public int Score;
    public bool HasWon;
    public bool HasFailed;

    private CanvasGroup winCanvasGroup;
    private CanvasGroup failCanvasGroup;
    private Coroutine winRoutine;
    private Coroutine failRoutine;

    private float sliderTargetValue;

    void Start()
    {
        PreparePanel(winObject, startScale, out winCanvasGroup);
        PreparePanel(failedObject, failStartScale, out failCanvasGroup);

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

    private void PreparePanel(GameObject panel, float initialScale, out CanvasGroup cg)
    {
        cg = null;
        if (!panel) return;

        cg = panel.GetComponent<CanvasGroup>();
        if (!cg) cg = panel.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        panel.transform.localScale = Vector3.one * initialScale;
        panel.SetActive(false);
    }

    private void SetupSlider()
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

    private void UpdateLevelText()
    {
        if (!levelText) return;

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        int levelNumber = buildIndex + levelNumberOffset;
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

        if (winRoutine != null) StopCoroutine(winRoutine);
        winRoutine = null;

        if (winObject) winObject.SetActive(false);

        if (!failedObject) return;

        if (failRoutine != null) StopCoroutine(failRoutine);
        failRoutine = StartCoroutine(FailSequence());
    }

    private void ForceSliderFull()
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

    private IEnumerator WinSequence()
    {
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

        yield return AnimatePanel(winObject, winCanvasGroup, startScale, fadeDuration, scaleDuration);
    }

    private IEnumerator FailSequence()
    {
        if (!failedObject) yield break;

        failedObject.SetActive(true);
        if (!failCanvasGroup) failCanvasGroup = failedObject.GetComponent<CanvasGroup>();

        yield return AnimatePanel(failedObject, failCanvasGroup, failStartScale, failFadeDuration, failScaleDuration);
    }

    private IEnumerator AnimatePanel(GameObject panel, CanvasGroup cg, float fromScale, float fadeDur, float scaleDur)
    {
        cg.alpha = 0f;
        panel.transform.localScale = Vector3.one * fromScale;

        float fadeT = 0f;
        float scaleT = 0f;

        while (fadeT < fadeDur || scaleT < scaleDur)
        {
            if (fadeT < fadeDur)
            {
                fadeT += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(fadeT / fadeDur);
            }

            if (scaleT < scaleDur)
            {
                scaleT += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(scaleT / scaleDur);
                float eased = EaseOutBack(t);
                float s = Mathf.Lerp(fromScale, 1f, eased);
                panel.transform.localScale = Vector3.one * s;
            }

            yield return null;
        }

        cg.alpha = 1f;
        panel.transform.localScale = Vector3.one;
    }

    private float EaseOutBack(float x)
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
        if (failRoutine != null) StopCoroutine(failRoutine);
        winRoutine = null;
        failRoutine = null;

        if (winParticles)
            winParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        PreparePanel(winObject, startScale, out winCanvasGroup);
        PreparePanel(failedObject, failStartScale, out failCanvasGroup);

        SetupSlider();
        RefreshUI(immediateSlider: true);
    }

    private void RefreshUI(bool immediateSlider)
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