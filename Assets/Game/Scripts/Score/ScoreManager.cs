using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    public int Score { get; private set; }
    public bool HasWon { get; private set; }
    public bool HasFailed { get; private set; }

    private CanvasGroup winCanvasGroup;
    private CanvasGroup failCanvasGroup;

    private Coroutine winRoutine;
    private Coroutine failRoutine;

    private float sliderTargetValue;

    void Start()
    {
        SetupPanels();
        SetupSlider();
        UpdateUI(true);
    }

    void Update()
    {
        if (progressSlider != null && smoothSlider)
        {
            progressSlider.value = Mathf.Lerp(progressSlider.value, sliderTargetValue, Time.unscaledDeltaTime * sliderSmoothSpeed);
        }
    }

    private void SetupPanels()
    {
        if (winObject != null)
        {
            winCanvasGroup = winObject.GetComponent<CanvasGroup>();
            if (winCanvasGroup == null)
            {
                winCanvasGroup = winObject.AddComponent<CanvasGroup>();
            }

            winCanvasGroup.alpha = 0f;
            winObject.transform.localScale = Vector3.one * startScale;
            winObject.SetActive(false);
        }

        if (failedObject != null)
        {
            failCanvasGroup = failedObject.GetComponent<CanvasGroup>();
            if (failCanvasGroup == null)
            {
                failCanvasGroup = failedObject.AddComponent<CanvasGroup>();
            }

            failCanvasGroup.alpha = 0f;
            failedObject.transform.localScale = Vector3.one * failStartScale;
            failedObject.SetActive(false);
        }
    }

    private void SetupSlider()
    {
        if (progressSlider == null)
        {
            return;
        }

        progressSlider.minValue = 0f;

        if (sliderUseNormalized)
        {
            progressSlider.maxValue = 1f;
        }
        else
        {
            progressSlider.maxValue = targetScore;
        }
    }

    public void SetLevelNumber(int levelNumber)
    {
        if (levelText == null)
        {
            return;
        }

        levelText.text = $"{levelPrefix} {levelNumber} {levelSuffix}";
    }

    public void AddScore(int amount)
    {
        if (HasWon || HasFailed)
        {
            return;
        }

        if (amount <= 0)
        {
            return;
        }

        Score += amount;

        UpdateUI(!smoothSlider);

        if (Score >= targetScore)
        {
            HasWon = true;

            ForceSliderFull();

            if (failRoutine != null)
            {
                StopCoroutine(failRoutine);
                failRoutine = null;
            }

            if (failedObject != null)
            {
                failedObject.SetActive(false);
            }

            if (winRoutine != null)
            {
                StopCoroutine(winRoutine);
            }

            winRoutine = StartCoroutine(WinSequence());
        }
    }

    public void ShowFailed()
    {
        if (HasWon || HasFailed)
        {
            return;
        }

        HasFailed = true;

        if (winRoutine != null)
        {
            StopCoroutine(winRoutine);
            winRoutine = null;
        }

        if (winObject != null)
        {
            winObject.SetActive(false);
        }

        if (failedObject == null)
        {
            return;
        }

        if (failRoutine != null)
        {
            StopCoroutine(failRoutine);
        }

        failRoutine = StartCoroutine(FailSequence());
    }

    private void ForceSliderFull()
    {
        if (progressSlider == null)
        {
            return;
        }

        if (sliderUseNormalized)
        {
            sliderTargetValue = 1f;

            if (!smoothSlider)
            {
                progressSlider.value = 1f;
            }
        }
        else
        {
            progressSlider.maxValue = targetScore;
            sliderTargetValue = targetScore;

            if (!smoothSlider)
            {
                progressSlider.value = targetScore;
            }
        }
    }

    private IEnumerator WinSequence()
    {
        if (winParticles != null)
        {
            winParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            winParticles.Play();
        }

        if (particlesDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(particlesDelay);
        }

        if (winObject == null)
        {
            yield break;
        }

        winObject.SetActive(true);

        if (winCanvasGroup == null)
        {
            winCanvasGroup = winObject.GetComponent<CanvasGroup>();
        }

        if (winCanvasGroup == null)
        {
            winCanvasGroup = winObject.AddComponent<CanvasGroup>();
        }

        winCanvasGroup.alpha = 0f;
        winObject.transform.localScale = Vector3.one * startScale;

        float fadeTime = 0f;
        float scaleTime = 0f;

        while (fadeTime < fadeDuration || scaleTime < scaleDuration)
        {
            if (fadeTime < fadeDuration)
            {
                fadeTime += Time.unscaledDeltaTime;
                winCanvasGroup.alpha = Mathf.Clamp01(fadeTime / fadeDuration);
            }

            if (scaleTime < scaleDuration)
            {
                scaleTime += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(scaleTime / scaleDuration);
                float eased = EaseOutBack(t);

                float value = Mathf.Lerp(startScale, 1f, eased);
                winObject.transform.localScale = Vector3.one * value;
            }

            yield return null;
        }

        winCanvasGroup.alpha = 1f;
        winObject.transform.localScale = Vector3.one;
    }

    private IEnumerator FailSequence()
    {
        if (failedObject == null)
        {
            yield break;
        }

        failedObject.SetActive(true);

        if (failCanvasGroup == null)
        {
            failCanvasGroup = failedObject.GetComponent<CanvasGroup>();
        }

        if (failCanvasGroup == null)
        {
            failCanvasGroup = failedObject.AddComponent<CanvasGroup>();
        }

        failCanvasGroup.alpha = 0f;
        failedObject.transform.localScale = Vector3.one * failStartScale;

        float fadeTime = 0f;
        float scaleTime = 0f;

        while (fadeTime < failFadeDuration || scaleTime < failScaleDuration)
        {
            if (fadeTime < failFadeDuration)
            {
                fadeTime += Time.unscaledDeltaTime;
                failCanvasGroup.alpha = Mathf.Clamp01(fadeTime / failFadeDuration);
            }

            if (scaleTime < failScaleDuration)
            {
                scaleTime += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(scaleTime / failScaleDuration);
                float eased = EaseOutBack(t);

                float value = Mathf.Lerp(failStartScale, 1f, eased);
                failedObject.transform.localScale = Vector3.one * value;
            }

            yield return null;
        }

        failCanvasGroup.alpha = 1f;
        failedObject.transform.localScale = Vector3.one;
    }

    private float EaseOutBack(float x)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    public void ResetScore()
    {
        Score = 0;
        HasWon = false;
        HasFailed = false;

        if (winRoutine != null)
        {
            StopCoroutine(winRoutine);
            winRoutine = null;
        }

        if (failRoutine != null)
        {
            StopCoroutine(failRoutine);
            failRoutine = null;
        }

        if (winParticles != null)
        {
            winParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (winObject != null)
        {
            if (winCanvasGroup == null)
            {
                winCanvasGroup = winObject.GetComponent<CanvasGroup>();
            }

            if (winCanvasGroup == null)
            {
                winCanvasGroup = winObject.AddComponent<CanvasGroup>();
            }

            winCanvasGroup.alpha = 0f;
            winObject.transform.localScale = Vector3.one * startScale;
            winObject.SetActive(false);
        }

        if (failedObject != null)
        {
            if (failCanvasGroup == null)
            {
                failCanvasGroup = failedObject.GetComponent<CanvasGroup>();
            }

            if (failCanvasGroup == null)
            {
                failCanvasGroup = failedObject.AddComponent<CanvasGroup>();
            }

            failCanvasGroup.alpha = 0f;
            failedObject.transform.localScale = Vector3.one * failStartScale;
            failedObject.SetActive(false);
        }

        SetupSlider();
        UpdateUI(true);
    }

    private void UpdateUI(bool immediateSlider)
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {Score}/{targetScore}";
        }

        if (progressSlider == null)
        {
            return;
        }

        if (sliderUseNormalized)
        {
            float value = (targetScore <= 0) ? 1f : Mathf.Clamp01((float)Score / (float)targetScore);
            sliderTargetValue = value;

            if (immediateSlider)
            {
                progressSlider.value = value;
            }
        }
        else
        {
            progressSlider.maxValue = targetScore;

            float value = Mathf.Clamp(Score, 0, targetScore);
            sliderTargetValue = value;

            if (immediateSlider)
            {
                progressSlider.value = value;
            }
        }
    }
}