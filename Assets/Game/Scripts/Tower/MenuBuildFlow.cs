using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuBuildFlow : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject holdToBuildButtonObject;
    [SerializeField] private GameObject nextLevelButtonObject;
    [SerializeField] private TMP_Text nextLevelButtonText;

    [Header("Flow")]
    [SerializeField] private SceneFlow sceneFlow;
    [SerializeField] private float fadeDuration = 0.25f;

    private CanvasGroup holdCanvasGroup;
    private CanvasGroup nextCanvasGroup;

    private bool wasShowingNextButton;

    private void Awake()
    {
        holdCanvasGroup = GetOrAddCanvasGroup(holdToBuildButtonObject);
        nextCanvasGroup = GetOrAddCanvasGroup(nextLevelButtonObject);
    }

    private void Start()
    {
        UpdateNextLevelText();
        RefreshButtonsImmediate();
    }

    private void Update()
    {
        UpdateNextLevelText();

        bool shouldShowHoldButton = TowerProgress.HasPendingStep();
        bool shouldShowNextButton = !shouldShowHoldButton;

        if (shouldShowNextButton != wasShowingNextButton)
        {
            wasShowingNextButton = shouldShowNextButton;
            StopAllCoroutines();
            StartCoroutine(SwitchButtonsSmooth(shouldShowHoldButton, shouldShowNextButton));
        }
    }

    private void RefreshButtonsImmediate()
    {
        bool shouldShowHoldButton = TowerProgress.HasPendingStep();
        bool shouldShowNextButton = !shouldShowHoldButton;

        SetCanvasState(holdCanvasGroup, holdToBuildButtonObject, shouldShowHoldButton, true);
        SetCanvasState(nextCanvasGroup, nextLevelButtonObject, shouldShowNextButton, true);

        wasShowingNextButton = shouldShowNextButton;
    }

    private System.Collections.IEnumerator SwitchButtonsSmooth(bool showHoldButton, bool showNextButton)
    {
        if (showHoldButton)
        {
            if (holdToBuildButtonObject != null)
            {
                holdToBuildButtonObject.SetActive(true);
            }
        }

        if (showNextButton)
        {
            if (nextLevelButtonObject != null)
            {
                nextLevelButtonObject.SetActive(true);
            }
        }

        float timer = 0f;

        float holdStart = holdCanvasGroup != null ? holdCanvasGroup.alpha : 0f;
        float nextStart = nextCanvasGroup != null ? nextCanvasGroup.alpha : 0f;

        float holdTarget = showHoldButton ? 1f : 0f;
        float nextTarget = showNextButton ? 1f : 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(timer / fadeDuration);

            if (holdCanvasGroup != null)
            {
                holdCanvasGroup.alpha = Mathf.Lerp(holdStart, holdTarget, t);
            }

            if (nextCanvasGroup != null)
            {
                nextCanvasGroup.alpha = Mathf.Lerp(nextStart, nextTarget, t);
            }

            yield return null;
        }

        SetCanvasState(holdCanvasGroup, holdToBuildButtonObject, showHoldButton, false);
        SetCanvasState(nextCanvasGroup, nextLevelButtonObject, showNextButton, false);
    }

    private void SetCanvasState(CanvasGroup canvasGroup, GameObject targetObject, bool isVisible, bool keepActiveIfHidden)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        if (targetObject != null)
        {
            if (isVisible)
            {
                targetObject.SetActive(true);
            }
            else
            {
                if (keepActiveIfHidden)
                {
                    targetObject.SetActive(true);
                }
                else
                {
                    targetObject.SetActive(false);
                }
            }
        }
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = targetObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = targetObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void UpdateNextLevelText()
    {
        if (nextLevelButtonText == null)
        {
            return;
        }

        int shownLevelNumber = GameProgress.CurrentLevelIndex + 1;
        nextLevelButtonText.text = "Level " + shownLevelNumber;
    }

  
}