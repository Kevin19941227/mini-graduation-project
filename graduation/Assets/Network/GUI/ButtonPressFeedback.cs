using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles pointer and keyboard submit feedback for one Unity UI Button.
/// </summary>
[DisallowMultipleComponent]
public sealed class ButtonPressFeedback : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler,
    ISubmitHandler
{
    private const float FadeInDuration = 0.03f;
    private const float RestoreDelay = 0.08f;
    private const float FadeOutDuration = 0.12f;

    private Button button;
    private Graphic targetGraphic;
    private Color pressedColor = Color.cyan;
    private Color colorBeforePress;
    private Coroutine restoreRoutine;
    private bool hasColorBeforePress;
    private bool isPressed;

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        isPressed = false;
    }

    private void OnDisable()
    {
        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        RestoreColorImmediately();
        hasColorBeforePress = false;
        isPressed = false;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Sets the visual color used while the button is being pressed.
    /// </summary>
    public void Configure(Color feedbackColor)
    {
        pressedColor = feedbackColor;
        CacheReferences();
    }

    #endregion

    #region Pointer Events

    public void OnPointerDown(PointerEventData eventData)
    {
        Press();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Press();
        Release();
    }

    #endregion

    #region Feedback

    private void Press()
    {
        if (!CanPlayFeedback())
        {
            return;
        }

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        CacheReferences();
        if (targetGraphic != null)
        {
            colorBeforePress = targetGraphic.color;
            hasColorBeforePress = true;
            targetGraphic.CrossFadeColor(pressedColor, FadeInDuration, true, true);
        }

        isPressed = true;
        ButtonFeedbackRuntime.PlayClick();
    }

    private void Release()
    {
        if (!isPressed)
        {
            return;
        }

        isPressed = false;

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
        }

        restoreRoutine = StartCoroutine(RestoreColorAfterDelay());
    }

    private IEnumerator RestoreColorAfterDelay()
    {
        yield return new WaitForSecondsRealtime(RestoreDelay);
        RestoreColor(FadeOutDuration);
        restoreRoutine = null;
        hasColorBeforePress = false;
    }

    private void RestoreColor(float duration)
    {
        if (targetGraphic != null && hasColorBeforePress)
        {
            targetGraphic.CrossFadeColor(colorBeforePress, duration, true, true);
        }
    }

    private void RestoreColorImmediately()
    {
        if (targetGraphic != null && hasColorBeforePress)
        {
            targetGraphic.color = colorBeforePress;
        }
    }

    private bool CanPlayFeedback()
    {
        CacheReferences();
        return button != null && button.IsInteractable();
    }

    private void CacheReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            targetGraphic = button.targetGraphic;
        }
    }

    #endregion
}
