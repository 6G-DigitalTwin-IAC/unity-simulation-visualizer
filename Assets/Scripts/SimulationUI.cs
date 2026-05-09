using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime control panel. Attach to a Canvas child object.
/// Wire up all fields in the Inspector.
/// </summary>
public class SimulationUI : MonoBehaviour
{
    [Header("Driver")]
    public SimulationDriver driver;

    [Header("Controls")]
    public Button  playPauseBtn;
    public Slider  speedSlider;        // value range: 0.5 → 10 (ticks/sec)
    public Button  forceRerouteBtn;
    public Button  randomFailureBtn;

    [Header("Metric Labels")]
    public TMP_Text labelDelay;
    public TMP_Text labelReroutes;
    public TMP_Text labelStability;
    public TMP_Text labelLinks;
    public TMP_Text labelLoad;
    public TMP_Text labelStep;
    public TMP_Text labelPath;
    public TMP_Text labelMode;

    [Header("Play Button Text")]
    public TMP_Text playBtnLabel;

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

        driver.OnStepComplete += Refresh;

        // Initial UI update
        if (playBtnLabel) playBtnLabel.text = driver.isPlaying ? "Pause" : "Play";

        Debug.Log("[SimulationUI] UI initialized successfully");
    }

    void OnDestroy() { if (driver) driver.OnStepComplete -= Refresh; }

    void OnPlayPause()
    {
        driver.TogglePlaying();
        playBtnLabel.text = driver.isPlaying ? "Pause" : "Play";
    }

    void OnRandomFailure()
    {
        // Fail a random active link
        var links = new System.Collections.Generic.List<SatelliteLink>(
            driver.Network.GetLinks().Values);
        var active = links.FindAll(l => l.active);
        if (active.Count == 0) return;
        var chosen = active[UnityEngine.Random.Range(0, active.Count)];
        driver.InjectFailure(chosen.u, chosen.v);
    }

    void Refresh(NetworkSnapshot snap)
    {
        if (snap == null) return;
        var m = snap.metrics;
        var r = snap.route;

        if (labelDelay)     labelDelay.text     = $"Avg Delay:   {m.avgDelay:F1} ms";
        if (labelReroutes)  labelReroutes.text  = $"Reroutes:    {m.rerouteCount}";
        if (labelStability) labelStability.text = $"Stability:   {m.stabilityScore:F0}%";
        if (labelLinks)     labelLinks.text     = $"Links:       {m.activeLinks} / {m.totalLinks}";
        if (labelLoad)      labelLoad.text      = $"Avg Load:    {m.avgLoad * 100:F0}%";
        if (labelStep)      labelStep.text      = $"Step:        {snap.stepId}";
        if (labelMode) labelMode.text = r.isAdaptive ? "ADAPTIVE" : "STATIC";
        if (labelPath)
            labelPath.text = r.isActive
                ? "Path: " + string.Join(" > ", r.path)
                : "Path: [NO ROUTE]";
    }
}