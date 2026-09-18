using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// k-NN regression over the swept (telemetry_delay_s, load_sigma) -> best_threshold
/// tables from the Adaptive-System-Algorithm-Notebook repo (data/training_table_*_merged.csv),
/// reproduced natively in C# — no external ML runtime needed.
/// Only ever takes the twin's own operating-point parameters as input; never touches ground truth.
/// </summary>
public class AdaptiveThresholdSelector
{
    private struct Sample { public float delay, sigma, threshold; }

    private static List<Sample> calmTable;
    private static List<Sample> burstyTable;
    private static bool loaded;

    private const int K = 5;

    public AdaptiveThresholdSelector()
    {
        if (!loaded) LoadTables();
    }

    private static void LoadTables()
    {
        calmTable   = ParseCsv(Resources.Load<TextAsset>("GateData/gate_threshold_calm"));
        burstyTable = ParseCsv(Resources.Load<TextAsset>("GateData/gate_threshold_bursty"));
        loaded = true;

        if (calmTable.Count == 0 || burstyTable.Count == 0)
            Debug.LogWarning("[AdaptiveThresholdSelector] Gate threshold tables missing or empty under Resources/GateData — adaptive gate will fall back to 0.8.");
    }

    private static List<Sample> ParseCsv(TextAsset asset)
    {
        var list = new List<Sample>();
        if (asset == null) return list;

        var lines = asset.text.Split('\n');
        for (int i = 1; i < lines.Length; i++) // skip header row
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;

            var cols = line.Split(',');
            if (cols.Length < 3) continue;

            list.Add(new Sample
            {
                delay     = float.Parse(cols[0], CultureInfo.InvariantCulture),
                sigma     = float.Parse(cols[1], CultureInfo.InvariantCulture),
                threshold = float.Parse(cols[2], CultureInfo.InvariantCulture),
            });
        }
        return list;
    }

    /// <summary>Inverse-distance-weighted k-NN over the swept table for this traffic regime.</summary>
    public float GetThreshold(float delaySeconds, float loadSigma, TrafficRegime regime)
    {
        var table = regime == TrafficRegime.Bursty ? burstyTable : calmTable;
        if (table == null || table.Count == 0) return 0.8f;

        var scored = new List<(float dist, float thr)>(table.Count);
        foreach (var s in table)
        {
            float dd = (s.delay - delaySeconds) / 30f;
            float ds = (s.sigma - loadSigma) / 0.7f;
            scored.Add((Mathf.Sqrt(dd * dd + ds * ds), s.threshold));
        }
        scored.Sort((a, b) => a.dist.CompareTo(b.dist));

        int k = Mathf.Min(K, scored.Count);
        float wSum = 0f, tSum = 0f;
        for (int i = 0; i < k; i++)
        {
            float w = 1f / (scored[i].dist + 0.01f);
            wSum += w;
            tSum += w * scored[i].thr;
        }
        return Mathf.Clamp(tSum / wSum, 0.5f, 0.95f);
    }
}
