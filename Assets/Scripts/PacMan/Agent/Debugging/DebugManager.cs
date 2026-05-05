using System;
using System.Linq;
using UnityEngine;
using Scripts.Map;
using UnityEngine.Serialization;


namespace PacMan.Agent.Debugging
{
    /// <summary>
    /// Singleton debug hub for Pac-Man agent visualizations and aggregate timing capture.
    /// </summary>
    public class DebugManager : MonoBehaviour
    {
        // The Singleton instance
        public static DebugManager Instance { get; private set; }

        private readonly PacManTimingKeeper _timingKeeper = new();

        [Header("PacMan Debugging")]
        [Tooltip("Show the generated Obstacle Grid Map")]
        public bool obstacleMap;

        [Tooltip("Show the Division of the Middle")]
        public bool middle;
        
        [Tooltip("Show the Voronoi Partitioning")]
        public bool voronoi;
        
        [Tooltip("Show Pathfinding Planner steps")]
        public bool aStar;

        [Tooltip("Show the Current Path")]
        public bool path;

        [Tooltip("Show the Velocity Obstacle Debugger")]
        public bool vO;

        [Tooltip("Show the Enemy Localization Debugger")]
        public bool enemyLocalization;

        [Tooltip("Show role/mode/reason labels above agents")]
        public bool agentHud = true;

        [Header("Timing Debugging")]
        [Tooltip("Enable aggregate timing capture for AI hot paths like Voronoi and A*")]
        public bool timeKeeper;

        [Tooltip("Log a compact timing summary every N seconds while the run is active")]
        [FormerlySerializedAs("timeKeeperLogIntervalSeconds")]
        public float timingLogIntervalSeconds = 5f;

        [Tooltip("Emit a terminal/console warning when a single timing sample exceeds this threshold")]
        public float timingLongSampleThresholdMs = 25f;

        private void Awake()
        {
            // Standard Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                // Optional: Keep this alive across scene loads
                DontDestroyOnLoad(gameObject); 
            }
        }

        /// <summary>
        /// Starts a timing scope for a named agent hot path when timing capture is enabled.
        /// </summary>
        /// <param name="sectionName">The label used to group timing samples, such as <c>Voronoi</c> or <c>A* Path</c>.</param>
        /// <returns>
        /// A disposable scope that records the elapsed time on disposal, or a default scope when timing is disabled.
        /// </returns>
        public static TimingScope BeginTimingScope(string sectionName)
        {
            if (Instance == null || !Instance.timeKeeper)
            {
                return default;
            }

            return Instance._timingKeeper.BeginScope(sectionName);
        }

        /// <summary>
        /// Clears the current timing aggregates and starts a fresh capture session.
        /// </summary>
        /// <param name="reason">Optional context string recorded in the next snapshot summary.</param>
        public void ResetTimingForNewRun(string reason = null)
        {
            _timingKeeper.ResetForNewRun();
            if (timeKeeper)
            {
                EmitTimingLog($"[Timing] Reset run capture{(string.IsNullOrWhiteSpace(reason) ? string.Empty : $" | {reason}")}");
            }
        }

        /// <summary>
        /// Updates the simulation-time value associated with the current timing snapshot.
        /// </summary>
        /// <param name="simulationTimeSeconds">The current simulation time in seconds.</param>
        public void SetTimingSimulationTime(float simulationTimeSeconds)
        {
            _timingKeeper.SetSimulationTime(simulationTimeSeconds);
        }

        /// <summary>
        /// Gets the current live or frozen timing snapshot.
        /// </summary>
        /// <returns>The current aggregate timing snapshot.</returns>
        public TimingSnapshot GetTimingSnapshot()
        {
            return _timingKeeper.GetSnapshot();
        }

        /// <summary>
        /// Gets a value indicating whether timing data has been collected.
        /// </summary>
        public bool HasTimingData => _timingKeeper.HasData;

        /// <summary>
        /// Freezes the current timing session and returns the last aggregate snapshot.
        /// </summary>
        /// <param name="reason">Optional context string included in the frozen snapshot and log output.</param>
        /// <returns>The frozen timing snapshot.</returns>
        public TimingSnapshot FreezeTimingCapture(string reason)
        {
            var snapshot = _timingKeeper.Freeze(reason);
            if (timeKeeper && snapshot.HasData)
            {
                EmitTimingLog($"[Timing] Final summary{(string.IsNullOrWhiteSpace(reason) ? string.Empty : $" | {reason}")} | run={snapshot.RunElapsedSeconds:0.0}s | sim={snapshot.SimulationTimeSeconds:0.0}s | {snapshot.BuildCompactSummary(8)}");
                EmitTimingLog(snapshot.BuildAsciiBarPlot());
            }

            return snapshot;
        }

        /// <summary>
        /// Writes a timing message to both the Unity console and the terminal output stream.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void EmitTimingLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.Log(message);
            Console.WriteLine(message);
        }

        /// <summary>
        /// Draws the in-game timing overlay when timing capture is enabled.
        /// </summary>
        private void OnGUI()
        {
            if (!timeKeeper)
            {
                return;
            }

            var snapshot = GetTimingSnapshot();
            if (!snapshot.HasData)
            {
                return;
            }

            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            var rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = Color.white }
            };

            GUILayout.BeginArea(new Rect(12f, 12f, 520f, 260f), GUI.skin.box);
            GUILayout.Label(snapshot.IsLive ? "Timing summary (live)" : "Timing summary (frozen)", headerStyle);
            GUILayout.Label($"Run {snapshot.RunElapsedSeconds:0.0}s | sim {snapshot.SimulationTimeSeconds:0.0}s", rowStyle);

            var sections = snapshot.Sections.OrderByDescending(section => section.TotalMilliseconds).Take(6).ToArray();
            var maxTotalMs = Mathf.Max(0.0001f, sections.Max(section => (float)section.TotalMilliseconds));

            foreach (var section in sections)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(section.Name, rowStyle, GUILayout.Width(110f));
                var barRect = GUILayoutUtility.GetRect(180f, 14f);
                GUI.Box(barRect, GUIContent.none);

                var fillWidth = Mathf.Clamp((float)(section.TotalMilliseconds / maxTotalMs) * barRect.width, 2f, barRect.width);
                var previousColor = GUI.color;
                GUI.color = new Color(0.35f, 0.75f, 1f, 0.9f);
                GUI.Box(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), GUIContent.none);
                GUI.color = previousColor;

                GUILayout.Label($"{section.TotalMilliseconds:0.00} ms", rowStyle, GUILayout.Width(85f));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Freezes the current timing capture when the application quits.
        /// </summary>
        private void OnApplicationQuit()
        {
            FreezeTimingCapture("application quit");
        }
        
        private void OnDrawGizmos()
        {
            // If the toggle is on, and the map has finished generating somewhere in the game
            if (obstacleMap && ObstacleMapV2.Instance != null)
            {
                // Tell the map to draw itself!
                ObstacleMapV2.Instance.DrawMapGizmos();
            }
        }
    }
}
