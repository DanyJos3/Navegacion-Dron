using System;
using System.IO;
using System.Globalization;
using UnityEngine;

public class DroneEvaluationLogger : MonoBehaviour
{
    public string filePrefix = "drone_episodes_v04_preservado";
    public bool logEveryEpisode = false;

    private string csvPath;
    private int episodeCount;
    private bool initialized;

    private void Awake() => EnsureInitialized();

    public void EnsureInitialized()
    {
        if (initialized) return;
        try
        {
#if UNITY_EDITOR
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evaluations"));
#else
            string directory = Path.Combine(Application.persistentDataPath, "Evaluations");
#endif
            Directory.CreateDirectory(directory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string safeName = SanitizeFileName(gameObject.name);
            csvPath = Path.Combine(directory, $"{filePrefix}_{safeName}_{timestamp}.csv");
            File.WriteAllText(csvPath,
                "episode,result,success,collisions,steps,reward,distance_traveled,initial_distance," +
                "final_distance,minimum_distance,maximum_distance,allowed_distance," +
                "path_efficiency\n");
            initialized = true;
            Debug.Log("[CSV CREATED] " + csvPath, this);
        }
        catch (Exception ex)
        {
            Debug.LogError("[CSV ERROR] Failed to initialize: " + ex, this);
        }
    }

    public void RecordEpisode(string result, int steps, float reward,
        float distanceTraveled, float initialDistance, float finalDistance,
        float minimumDistance, float maximumDistance, float allowedDistance)
    {
        EnsureInitialized();
        if (!initialized) return;
        int success = result == "success" ? 1 : 0;
        // DroneAgent ends the episode on the first collision, so it is 0 or 1.
        int collisions = result == "collision" ? 1 : 0;
        float efficiency = distanceTraveled > 0.0001f
            ? initialDistance / distanceTraveled : 0f;
        // Efficiency is a straight-line reference, not a collision-free optimal path.
        string row = string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5:F6},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4}\n",
            episodeCount + 1, result, success, collisions, steps, reward, distanceTraveled,
            initialDistance, finalDistance, minimumDistance,
            maximumDistance, allowedDistance, efficiency);
        try
        {
            File.AppendAllText(csvPath, row);
            episodeCount++;
            if (logEveryEpisode)
                Debug.Log($"[CSV SAVED] Episode={episodeCount}, result={result}, file={csvPath}", this);
        }
        catch (Exception ex)
        {
            Debug.LogError("[CSV ERROR] Failed to write episode: " + ex, this);
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Replace(' ', '_');
    }

    public string GetFilePath() => csvPath;
}
