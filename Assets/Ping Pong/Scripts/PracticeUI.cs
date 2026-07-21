using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PracticeUI : MonoBehaviour
{
    public static PracticeUI Instance { get; private set; }

    public KeyCode toggleKey = KeyCode.P;
    public PracticeModeManager manager;

    private GameObject canvasGO;
    private PracticeBallListener listener;

    // UI elements
    private TextMeshProUGUI speedText;
    private TextMeshProUGUI bouncesText;
    private TextMeshProUGUI maxComboText;

    private Slider initialSpeedSlider;
    private Slider maxSpeedSlider;
    private Slider gravitySlider;
    private Slider racketForceSlider;
    private bool   isInitialized = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (manager == null)
            manager = PracticeModeManager.Instance;

        if (manager != null && !isInitialized)
            Initialize(manager);
    }

    public void Initialize(PracticeModeManager practiceManager)
    {
        if (practiceManager == null || isInitialized) return;

        manager = practiceManager;

        if (manager.ball != null)
        {
            listener = manager.ball.GetComponent<PracticeBallListener>();
            if (listener == null)
                listener = manager.ball.gameObject.AddComponent<PracticeBallListener>();
        }

        CreateUI();
        UpdateUIValues();
        isInitialized = true;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (canvasGO != null)
            Destroy(canvasGO);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && canvasGO != null)
            canvasGO.SetActive(!canvasGO.activeSelf);

        if (canvasGO != null && canvasGO.activeSelf)
        {
            UpdateInfoTexts();
        }
    }

    void CreateUI()
    {
        // Canvas
        canvasGO = new GameObject("PracticeUI_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel
        var panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);
        var img = panelGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        var rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.02f, 0.02f);
        rt.anchorMax = new Vector2(0.30f, 0.6f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panelGO.transform, false);
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.fontSize = 20;
        title.text = "Modo Práctica — Configuración";
        var tr = titleGO.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0.92f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(8f, -6f);
        tr.offsetMax = new Vector2(-8f, 0f);

        float y = 0.78f;
        // Sliders
        initialSpeedSlider = CreateLabeledSlider(panelGO.transform, "Vel. inicial", y, manager.ball != null ? manager.ball.initialSpeed : 8f, 1f, 60f, (v) => { manager.SetBallInitialSpeed(v); UpdateSliderValueText(initialSpeedSlider, v); });
        y -= 0.12f;
        maxSpeedSlider = CreateLabeledSlider(panelGO.transform, "Vel. máxima", y, manager.ball != null ? manager.ball.maxSpeed : 25f, 5f, 400f, (v) => { manager.SetBallMaxSpeed(v); UpdateSliderValueText(maxSpeedSlider, v); });
        y -= 0.12f;
        gravitySlider = CreateLabeledSlider(panelGO.transform, "Gravedad", y, manager.gravity, 0f, 50f, (v) => { manager.gravity = v; UpdateSliderValueText(gravitySlider, v); });
        y -= 0.12f;
        racketForceSlider = CreateLabeledSlider(panelGO.transform, "Fuerza raqueta", y, manager.playerRaqueta != null ? manager.playerRaqueta.multiplicadorGolpe : 1.5f, 0.5f, 5f, (v) => { manager.SetPlayerRacketForce(v); UpdateSliderValueText(racketForceSlider, v); });
        y -= 0.16f;

        // Buttons
        CreateButton(panelGO.transform, "Lanzar pelota", y, () => LaunchBallNow());
        y -= 0.08f;
        CreateButton(panelGO.transform, "Reiniciar centro", y, () => { if (manager.ball != null) manager.ball.ResetBall(); });
        y -= 0.08f;
        CreateButton(panelGO.transform, "Detener pelota", y, () => { if (manager.ball != null) { var rb = manager.ball.GetComponent<Rigidbody>(); if (rb!=null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; } } });
        y -= 0.08f;
        CreateButton(panelGO.transform, "Reanudar pelota", y, () => { if (manager.ball != null) { var rb = manager.ball.GetComponent<Rigidbody>(); if (rb!=null) rb.isKinematic = false; } });
        y -= 0.12f;

        // Wall mode toggle
        var wallToggleGO = new GameObject("WallToggle", typeof(RectTransform));
        wallToggleGO.transform.SetParent(panelGO.transform, false);
        var wallToggle = wallToggleGO.AddComponent<Toggle>();
        wallToggle.isOn = !manager.wallPerfectBounce;
        wallToggle.onValueChanged.AddListener((v) => manager.SetWallRandomness(v));
        var wtRT = wallToggleGO.GetComponent<RectTransform>();
        if (wtRT == null) wtRT = wallToggleGO.AddComponent<RectTransform>();
        wtRT.anchorMin = new Vector2(0.05f, y);
        wtRT.anchorMax = new Vector2(0.95f, y + 0.06f);
        wtRT.offsetMin = Vector2.zero;
        wtRT.offsetMax = Vector2.zero;
        var wtLabelGO = new GameObject("WallLabel", typeof(RectTransform));
        wtLabelGO.transform.SetParent(wallToggleGO.transform, false);
        var wtText = wtLabelGO.AddComponent<TextMeshProUGUI>();
        wtText.gameObject.AddComponent<CanvasRenderer>();
        wtText.text = "Muro con aleatoriedad";
        wtText.fontSize = 14;

        // Info texts
        speedText = CreateInfoText(panelGO.transform, "Velocidad: 0", 0.06f);
        bouncesText = CreateInfoText(panelGO.transform, "Rebotes: 0", 0.02f);
        maxComboText = CreateInfoText(panelGO.transform, "Combo Max: 0", -0.02f);

        canvasGO.SetActive(false);
    }

    TextMeshProUGUI CreateInfoText(Transform parent, string text, float anchorY)
    {
        var go = new GameObject("Info", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.gameObject.AddComponent<CanvasRenderer>();
        t.text = text;
        t.fontSize = 14;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, anchorY);
        rt.anchorMax = new Vector2(0.95f, anchorY + 0.04f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return t;
    }

    void UpdateInfoTexts()
    {
        if (listener != null)
        {
            if (speedText != null)
                speedText.text = $"Velocidad: {listener.GetCurrentSpeed():F1}";
            if (bouncesText != null)
                bouncesText.text = $"Rebotes: {listener.GetBounces()}";
        }
        else
        {
            if (speedText != null)
                speedText.text = "Velocidad: -";
            if (bouncesText != null)
                bouncesText.text = "Rebotes: -";
        }

        int maxCombo = ComboManager.Instance != null ? ComboManager.Instance.MaxComboJugadorSession : 0;
        if (maxComboText != null)
            maxComboText.text = $"Combo Máx: {maxCombo}";
    }

    void UpdateSliderValueText(Slider slider, float value)
    {
        if (slider == null) return;
        var valueTransform = slider.transform.Find("ValueText");
        if (valueTransform == null) return;
        var text = valueTransform.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = $"{value:F1}";
    }

    public void ShowPracticeUI()
    {
        if (canvasGO != null)
            canvasGO.SetActive(true);
    }

    public void HidePracticeUI()
    {
        if (canvasGO != null)
            canvasGO.SetActive(false);
    }

    void UpdateUIValues()
    {
        if (initialSpeedSlider != null && manager != null && manager.ball != null)
        {
            initialSpeedSlider.value = manager.ball.initialSpeed;
            UpdateSliderValueText(initialSpeedSlider, manager.ball.initialSpeed);
        }
        if (maxSpeedSlider != null && manager != null && manager.ball != null)
        {
            maxSpeedSlider.value = manager.ball.maxSpeed;
            UpdateSliderValueText(maxSpeedSlider, manager.ball.maxSpeed);
        }
        if (gravitySlider != null && manager != null)
        {
            gravitySlider.value = manager.gravity;
            UpdateSliderValueText(gravitySlider, manager.gravity);
        }
        if (racketForceSlider != null && manager != null && manager.playerRaqueta != null)
        {
            racketForceSlider.value = manager.playerRaqueta.multiplicadorGolpe;
            UpdateSliderValueText(racketForceSlider, manager.playerRaqueta.multiplicadorGolpe);
        }
    }

    Slider CreateLabeledSlider(Transform parent, string label, float anchorY, float defaultValue, float min, float max, System.Action<float> onChanged)
    {
        var go = new GameObject(label + "_Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.name = label + "_Slider";
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, anchorY);
        rt.anchorMax = new Vector2(0.95f, anchorY + 0.08f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var txtGO = new GameObject("Label", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 14;
        txt.alignment = TextAlignmentOptions.Left;
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0f, 0.55f);
        txtRT.anchorMax = new Vector2(0.6f, 1f);
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        var valueGO = new GameObject("ValueText", typeof(RectTransform));
        valueGO.transform.SetParent(go.transform, false);
        var valueTxt = valueGO.AddComponent<TextMeshProUGUI>();
        valueTxt.text = $"{defaultValue:F1}";
        valueTxt.fontSize = 14;
        valueTxt.alignment = TextAlignmentOptions.Right;
        var valueRT = valueGO.GetComponent<RectTransform>();
        valueRT.anchorMin = new Vector2(0.6f, 0.55f);
        valueRT.anchorMax = new Vector2(1f, 1f);
        valueRT.offsetMin = Vector2.zero;
        valueRT.offsetMax = Vector2.zero;

        var sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderGO.transform.SetParent(go.transform, false);
        var sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0f, 0f);
        sliderRT.anchorMax = new Vector2(1f, 0.5f);
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        var sliderBackground = sliderGO.GetComponent<Image>();
        sliderBackground.color = new Color(1f, 1f, 1f, 0.14f);

        var slider = sliderGO.GetComponent<Slider>();
        slider.targetGraphic = sliderBackground;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;

        var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0.05f, 0.25f);
        fillAreaRT.anchorMax = new Vector2(0.95f, 0.75f);
        fillAreaRT.offsetMin = Vector2.zero;
        fillAreaRT.offsetMax = Vector2.zero;

        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        var fillImage = fillGO.GetComponent<Image>();
        fillImage.color = new Color(0.4f, 0.7f, 1f, 0.85f);

        var handleSlideAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlideAreaGO.transform.SetParent(sliderGO.transform, false);
        var handleAreaRT = handleSlideAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = Vector2.zero;
        handleAreaRT.offsetMax = Vector2.zero;

        var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(handleSlideAreaGO.transform, false);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0f, 0.15f);
        handleRT.anchorMax = new Vector2(0f, 0.85f);
        handleRT.sizeDelta = new Vector2(18f, 18f);
        var handleImage = handleGO.GetComponent<Image>();
        handleImage.color = new Color(1f, 1f, 1f, 0.9f);

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;

        slider.onValueChanged.AddListener((v) => { onChanged(v); valueTxt.text = $"{v:F1}"; });

        return slider;
    }

    void CreateButton(Transform parent, string text, float anchorY, System.Action onClick)
    {
        var go = new GameObject(text + "_Button");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, anchorY);
        rt.anchorMax = new Vector2(0.9f, anchorY + 0.06f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.12f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.gameObject.AddComponent<CanvasRenderer>();
        txt.text = text;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = 14;
    }

    void LaunchBallNow()
    {
        if (manager == null || manager.ball == null) return;
        var rb = manager.ball.GetComponent<Rigidbody>();
        if (rb == null) return;
        rb.isKinematic = false;
        rb.useGravity = true;
        Vector3 dir = Vector3.right * (Random.value > 0.5f ? 1f : -1f);
        Vector3 vel = dir.normalized * (manager.ball.initialSpeed);
        vel.y = manager.ball.racketLaunchUpward;
        rb.linearVelocity = vel;
    }
}
