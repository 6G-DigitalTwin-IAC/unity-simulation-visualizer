using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime control panel. Attach to a Canvas child object.
/// Wire up fields in the Inspector — every field is optional/null-checked, so the
/// simulation runs correctly even before new widgets described in the project plan
/// (mode buttons, regime toggle, delay/sigma sliders, comparison rows, gate flash)
/// have been built and wired in the Editor.
/// </summary>
public class SimulationUI : MonoBehaviour
{
    [Header("Driver")]
    public SimulationDriver driver;

    [Header("Playback Controls")]
    public Button  playPauseBtn;
    public Slider  speedSlider;        // value range: 0.5 → 10 (ticks/sec)
    public Button  forceRerouteBtn;
    public Button  randomFailureBtn;
    public TMP_Text playBtnLabel;

    [Header("Gate Mode Controls")]
    [Tooltip("Single button — each click cycles Direct -> Ungated -> Fixed -> Adaptive -> Direct. Its own label text is updated to show the current mode.")]
    public Button  gateModeToggleBtn;
    [Tooltip("Single button — each click cycles Calm -> Bursty -> Calm. Its own label text is updated to show the current regime.")]
    public Button  trafficRegimeToggleBtn;
    public Slider  telemetryDelaySlider;   // 1 - 30 seconds
    public Slider  loadSigmaSlider;        // 0.05 - 0.7

    [Header("Visualized-Mode Labels")]
    public TMP_Text labelDelay;
    public TMP_Text labelLinks;
    public TMP_Text labelLoad;
    public TMP_Text labelStep;
    public TMP_Text labelPath;
    public TMP_Text labelMode;
    public TMP_Text labelThreshold;
    public TMP_Text labelRegime;
    public TMP_Text labelTelemetryDelay;

    [Header("Table 1 style comparison — one row per mode")]
    [Tooltip("Order: Direct, Ungated, Fixed, Adaptive")]
    public TMP_Text[] comparisonRows; // each formatted as "Mode: N changes, X.XX J, YYms"

    [Header("Gate Flash Indicator")]
    public Image gateFlashImage;
    public Color colorGatePassed = new Color(0.2f, 0.9f, 0.3f, 1f);
    public Color colorGateBlocked = new Color(0.9f, 0.2f, 0.2f, 1f);
    public float gateFlashDuration = 0.3f;

    private static readonly GateMode[] ModeOrder =
        { GateMode.Direct, GateMode.Ungated, GateMode.Fixed, GateMode.Adaptive };

    private float gateFlashTimer;

    void Start()
    {
        if (driver == null)
        {
            driver = FindObjectOfType<SimulationDriver>();
            if (driver == null)
            {
                Debug.LogError("[SimulationUI] No SimulationDriver found in scene! UI will not function.");
                return;
            }
        }

        playPauseBtn   ?.onClick.AddListener(OnPlayPause);
        forceRerouteBtn?.onClick.AddListener(() => driver.ForceReroute());
        randomFailureBtn?.onClick.AddListener(OnRandomFailure);

        if (speedSlider)
        {
            speedSlider.minValue = 0.5f;
            speedSlider.maxValue = 10f;
            speedSlider.value    = 2.5f;
            speedSlider.onValueChanged.AddListener(v => driver.SetSpeed(v));
        }

        if (gateModeToggleBtn)
        {
            gateModeToggleBtn.onClick.AddListener(OnCycleGateMode);
            SetButtonLabel(gateModeToggleBtn, driver.config.gateMode.ToString());
        }

        if (trafficRegimeToggleBtn)
        {
            trafficRegimeToggleBtn.onClick.AddListener(OnCycleRegime);
            SetButtonLabel(trafficRegimeToggleBtn, driver.config.trafficRegime.ToString());
        }

        if (telemetryDelaySlider)
        {
            telemetryDelaySlider.minValue = 1f;
            telemetryDelaySlider.maxValue = 30f;
            telemetryDelaySlider.value    = driver.config.telemetryDelaySeconds;
            telemetryDelaySlider.onValueChanged.AddListener(v => driver.SetTelemetryDelay(v));
        }

        if (loadSigmaSlider)
        {
            loadSigmaSlider.minValue = 0.05f;
            loadSigmaSlider.maxValue = 0.7f;
            loadSigmaSlider.value    = driver.config.loadSigma;
            loadSigmaSlider.onValueChanged.AddListener(v => driver.SetLoadSigma(v));
        }

        driver.OnStepComplete += Refresh;

        if (playBtnLabel) playBtnLabel.text = driver.isPlaying ? "Pause" : "Play";
        if (gateFlashImage) gateFlashImage.enabled = false;

        Debug.Log("[SimulationUI] UI initialized successfully");
    }

    void OnDestroy() { if (driver) driver.OnStepComplete -= Refresh; }

    void Update()
    {
        if (gateFlashImage == null || gateFlashTimer <= 0f) return;
        gateFlashTimer -= Time.deltaTime;
        if (gateFlashTimer <= 0f) gateFlashImage.enabled = false;
    }

    void OnPlayPause()
    {
        driver.TogglePlaying();
        if (playBtnLabel) playBtnLabel.text = driver.isPlaying ? "Pause" : "Play";
    }

    void OnRandomFailure()
    {
        var links = driver.Network.GetLinks();
        var active = links.FindAll(l => l.active);
        if (active.Count == 0) return;
        var chosen = active[UnityEngine.Random.Range(0, active.Count)];
        driver.InjectFailure(chosen.u, chosen.v);
    }

    void OnCycleGateMode()
    {
        int idx = System.Array.IndexOf(ModeOrder, driver.config.gateMode);
        var next = ModeOrder[(idx + 1) % ModeOrder.Length];
        driver.SetGateMode(next);
        SetButtonLabel(gateModeToggleBtn, next.ToString());
    }

    void OnCycleRegime()
    {
        var next = driver.config.trafficRegime == TrafficRegime.Calm ? TrafficRegime.Bursty : TrafficRegime.Calm;
        driver.SetTrafficRegime(next);
        SetButtonLabel(trafficRegimeToggleBtn, next.ToString());
    }

    static void SetButtonLabel(Button btn, string text)
    {
        var label = btn.GetComponentInChildren<TMP_Text>();
        if (label) label.text = text;
    }

    void Refresh(NetworkSnapshot snap)
    {
        if (snap == null) return;
        var vis = snap.Visualized;

        if (labelDelay) labelDelay.text = $"Delay (path): {vis.trueDelayMs:F1} ms";
        if (labelLinks) labelLinks.text = $"Links: {snap.activeLinks} / {snap.totalLinks}";
        if (labelLoad)  labelLoad.text  = $"Avg Load: {snap.avgBackgroundLoad * 100:F0}%";
        if (labelStep)  labelStep.text  = $"Step: {snap.stepId}";
        if (labelMode)  labelMode.text  = snap.visualizedMode.ToString().ToUpperInvariant();
        if (labelPath)
            labelPath.text = vis.path != null && vis.path.Count > 1
                ? $"Hops: {vis.path.Count - 1}"
                : "Hops: [NO ROUTE]";
        if (labelThreshold)
            labelThreshold.text = snap.visualizedMode == GateMode.Fixed || snap.visualizedMode == GateMode.Adaptive
                ? $"tau = {vis.currentThreshold:F2}  (delta = {vis.lastDelta:F2})"
                : "tau = n/a";
        if (labelRegime) labelRegime.text = $"Traffic: {driver.config.trafficRegime}";
        if (labelTelemetryDelay) labelTelemetryDelay.text = $"Telemetry lag: {driver.config.telemetryDelaySeconds:F0}s";

        RefreshComparisonRows(snap);
        RefreshGateFlash(vis);
    }

    void RefreshComparisonRows(NetworkSnapshot snap)
    {
        if (comparisonRows == null) return;
        for (int i = 0; i < comparisonRows.Length && i < ModeOrder.Length; i++)
        {
            if (comparisonRows[i] == null) continue;
            var s = snap.modes[ModeOrder[i]];
            comparisonRows[i].text =
                $"{ModeOrder[i],-8}  {s.routeChanges,4} changes   {s.energyJoules,7:F2} J   {s.trueDelayMs,6:F1} ms";
        }
    }

    void RefreshGateFlash(GateModeStats vis)
    {
        if (gateFlashImage == null) return;
        if (!vis.lastProposed) return; // nothing to flash — the twin didn't propose a switch this tick

        gateFlashImage.enabled = true;
        gateFlashImage.color   = vis.lastAccepted ? colorGatePassed : colorGateBlocked;
        gateFlashTimer         = gateFlashDuration;
    }
}
