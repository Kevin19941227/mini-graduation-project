using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds click sound and pressed color feedback to every runtime Unity UI button.
/// </summary>
public sealed class ButtonFeedbackRuntime : MonoBehaviour
{
    private const string RuntimeObjectName = "[ButtonFeedbackRuntime]";
    private const float ScanInterval = 0.5f;
    private const float ClickDuration = 0.045f;
    private const int SampleRate = 44100;

    private static readonly Color PressedFeedbackColor = new Color(0.2f, 0.85f, 1f, 1f);
    private static readonly Color HighlightFeedbackColor = new Color(0.78f, 0.96f, 1f, 1f);

    private static ButtonFeedbackRuntime instance;

    private AudioSource audioSource;
    private AudioClip clickClip;
    private float nextScanTime;

    #region Runtime Bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static ButtonFeedbackRuntime EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);

        instance = runtimeObject.AddComponent<ButtonFeedbackRuntime>();
        return instance;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.75f;

        clickClip = CreateClickClip();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        InstallFeedbackOnAllButtons();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanTime)
        {
            return;
        }

        nextScanTime = Time.unscaledTime + ScanInterval;
        InstallFeedbackOnAllButtons();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Plays the shared UI click sound.
    /// </summary>
    public static void PlayClick()
    {
        ButtonFeedbackRuntime runtime = EnsureInstance();
        if (runtime.audioSource == null || runtime.clickClip == null)
        {
            return;
        }

        runtime.audioSource.pitch = Random.Range(0.96f, 1.04f);
        runtime.audioSource.PlayOneShot(runtime.clickClip);
    }

    /// <summary>
    /// Ensures a single button has the shared visual feedback component.
    /// </summary>
    public static void Install(Button button)
    {
        if (button == null)
        {
            return;
        }

        ButtonPressFeedback feedback = button.GetComponent<ButtonPressFeedback>();
        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<ButtonPressFeedback>();
        }

        feedback.Configure(PressedFeedbackColor);
        ApplyButtonColors(button);
    }

    #endregion

    #region Install

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallFeedbackOnAllButtons();
    }

    private static void InstallFeedbackOnAllButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Install(buttons[i]);
        }
    }

    private static void ApplyButtonColors(Button button)
    {
        ColorBlock colors = button.colors;
        colors.pressedColor = PressedFeedbackColor;
        colors.selectedColor = HighlightFeedbackColor;
        colors.colorMultiplier = Mathf.Max(colors.colorMultiplier, 1f);
        button.colors = colors;
    }

    #endregion

    #region Audio

    private static AudioClip CreateClickClip()
    {
        int sampleCount = Mathf.CeilToInt(SampleRate * ClickDuration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = (float)i / sampleCount;
            float envelope = Mathf.Exp(-progress * 18f);
            float highTone = Mathf.Sin(2f * Mathf.PI * 1600f * i / SampleRate);
            float lowTone = Mathf.Sin(2f * Mathf.PI * 780f * i / SampleRate);
            samples[i] = (highTone * 0.55f + lowTone * 0.45f) * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("Generated_Button_Click", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    #endregion
}
