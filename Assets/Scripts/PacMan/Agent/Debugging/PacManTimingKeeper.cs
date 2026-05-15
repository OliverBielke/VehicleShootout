using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PacMan.Agent.Debugging
{
    /// <summary>
    /// Collects aggregate timing samples for agent hot paths and exposes live and frozen snapshots.
    /// </summary>
    public sealed class PacManTimingKeeper
    {
        private const string TickTotalSectionName = "Total";

        private readonly Dictionary<string, TimingAggregate> _aggregates = new();
        private readonly object _lock = new();
        private float _runStartRealtime;
        private float _lastPeriodicLogRealtime;
        private float _simulationTimeSeconds;
        private bool _sessionActive;
        private bool _hasFrozenSnapshot;
        private TimingSnapshot _frozenSnapshot = TimingSnapshot.Empty;

        /// <summary>
        /// Clears all recorded samples and starts a fresh timing session.
        /// </summary>
        public void ResetForNewRun()
        {
            lock (_lock)
            {
                _aggregates.Clear();
                _runStartRealtime = Time.realtimeSinceStartup;
                _lastPeriodicLogRealtime = _runStartRealtime;
                _simulationTimeSeconds = 0f;
                _sessionActive = true;
                _hasFrozenSnapshot = false;
                _frozenSnapshot = TimingSnapshot.Empty;
            }
        }

        /// <summary>
        /// Stores the current simulation time for later reporting in snapshots and logs.
        /// </summary>
        /// <param name="simulationTimeSeconds">The simulation time in seconds.</param>
        public void SetSimulationTime(float simulationTimeSeconds)
        {
            lock (_lock)
            {
                _simulationTimeSeconds = Mathf.Max(0f, simulationTimeSeconds);
            }
        }

        /// <summary>
        /// Begins a timing scope for the given section name.
        /// </summary>
        /// <param name="sectionName">The section label used to group samples.</param>
        /// <returns>A disposable scope that records elapsed time when disposed.</returns>
        public TimingScope BeginScope(string sectionName)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                return default;
            }

            bool shouldAutoReset;
            lock (_lock)
            {
                shouldAutoReset = !_sessionActive;
            }

            if (shouldAutoReset)
            {
                ResetForNewRun();
            }

            return new TimingScope(this, sectionName.Trim(), Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Finalizes a timing scope and records the elapsed time into the aggregate totals.
        /// </summary>
        /// <param name="sectionName">The section label associated with the scope.</param>
        /// <param name="startTimestamp">The timestamp captured when the scope started.</param>
        internal void EndScope(string sectionName, long startTimestamp)
        {
            var manager = DebugManager.Instance;
            if (manager == null || !manager.timeKeeper || string.IsNullOrWhiteSpace(sectionName))
            {
                return;
            }

            var elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

            TimingSnapshot periodicSnapshot = null;
            string longSampleMessage = null;

            lock (_lock)
            {
                if (!_sessionActive)
                {
                    return;
                }

                if (!_aggregates.TryGetValue(sectionName, out var aggregate))
                {
                    aggregate = new TimingAggregate(sectionName);
                    _aggregates.Add(sectionName, aggregate);
                }

                aggregate.Add(elapsedMs);

                if (elapsedMs >= manager.timingLongSampleThresholdMs)
                {
                    longSampleMessage = BuildLongSampleMessage(sectionName, elapsedMs);
                }

                var interval = Mathf.Max(0f, manager.timingLogIntervalSeconds);
                if (interval > 0f && Time.realtimeSinceStartup - _lastPeriodicLogRealtime >= interval)
                {
                    _lastPeriodicLogRealtime = Time.realtimeSinceStartup;
                    periodicSnapshot = CreateSnapshotLocked(null);
                }
            }

            if (!string.IsNullOrEmpty(longSampleMessage))
            {
                DebugManager.EmitTimingLog(longSampleMessage);
            }

            if (periodicSnapshot != null)
            {
                DebugManager.EmitTimingLog(BuildPeriodicSummaryMessage(periodicSnapshot));
            }
        }

        /// <summary>
        /// Freezes the current session so future reads return the same snapshot.
        /// </summary>
        /// <param name="reason">Optional text stored with the frozen snapshot.</param>
        /// <returns>The frozen snapshot.</returns>
        public TimingSnapshot Freeze(string reason)
        {
            lock (_lock)
            {
                if (_hasFrozenSnapshot)
                {
                    return _frozenSnapshot;
                }

                _sessionActive = false;
                _hasFrozenSnapshot = true;
                _frozenSnapshot = CreateSnapshotLocked(reason);
                return _frozenSnapshot;
            }
        }

        /// <summary>
        /// Gets the current snapshot for the active or frozen session.
        /// </summary>
        /// <returns>The current timing snapshot.</returns>
        public TimingSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                if (_sessionActive)
                {
                    return CreateSnapshotLocked(null);
                }

                return _hasFrozenSnapshot ? _frozenSnapshot : TimingSnapshot.Empty;
            }
        }

        /// <summary>
        /// Gets a value indicating whether any timing samples have been collected.
        /// </summary>
        public bool HasData
        {
            get
            {
                lock (_lock)
                {
                    return _aggregates.Count > 0 || _hasFrozenSnapshot && _frozenSnapshot.HasData;
                }
            }
        }

        /// <summary>
        /// Creates a snapshot from the currently accumulated samples.
        /// </summary>
        /// <param name="reason">Optional text stored with the snapshot.</param>
        /// <returns>A snapshot of the current aggregate timing state.</returns>
        private TimingSnapshot CreateSnapshotLocked(string reason)
        {
            var simulationTimeSeconds = _simulationTimeSeconds > 0f
                ? _simulationTimeSeconds
                : Time.realtimeSinceStartup - _runStartRealtime;

            var runElapsedSeconds = Time.realtimeSinceStartup - _runStartRealtime;

            var sections = _aggregates.Values
                .Select(aggregate => aggregate.ToSnapshot())
                .OrderByDescending(section => section.TotalMilliseconds)
                .ThenBy(section => section.Name)
                .ToArray();

            var totalRunMilliseconds = Math.Max(0.0, runElapsedSeconds * 1000.0);
            var totalTickMilliseconds = ResolveTotalTickMilliseconds(sections);
            var totalTrackedMilliseconds = ResolveTrackedMilliseconds(sections);

            return new TimingSnapshot(
                reason,
                runElapsedSeconds,
                simulationTimeSeconds,
                sections,
                _sessionActive,
                Time.realtimeSinceStartup,
                totalTrackedMilliseconds,
                totalRunMilliseconds,
                totalTickMilliseconds);
        }

        private static double ResolveTotalTickMilliseconds(TimingSectionSnapshot[] sections)
        {
            if (sections == null || sections.Length == 0)
            {
                return 0.0;
            }

            var totalTickSection = sections.FirstOrDefault(section => string.Equals(section.Name, TickTotalSectionName, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(totalTickSection.Name))
            {
                return Math.Max(0.0, totalTickSection.TotalMilliseconds);
            }

            return Math.Max(0.0, sections.Max(section => section.TotalMilliseconds));
        }

        private static double ResolveTrackedMilliseconds(TimingSectionSnapshot[] sections)
        {
            if (sections == null || sections.Length == 0)
            {
                return 0.0;
            }

            return Math.Max(0.0, sections
                .Where(section => !string.Equals(section.Name, TickTotalSectionName, StringComparison.Ordinal))
                .Sum(section => section.TotalMilliseconds));
        }

        /// <summary>
        /// Builds a log message for a single slow timing sample.
        /// </summary>
        /// <param name="sectionName">The name of the timed section.</param>
        /// <param name="elapsedMs">The sample duration in milliseconds.</param>
        /// <returns>A formatted slow-sample log message.</returns>
        private string BuildLongSampleMessage(string sectionName, double elapsedMs)
        {
            var snapshot = GetSnapshot();
            return $"[Timing] Slow sample | section={sectionName} | sample={elapsedMs:0.00} ms | run={snapshot.RunElapsedSeconds:0.0}s | sim={snapshot.SimulationTimeSeconds:0.0}s | {snapshot.BuildCompactSummary(3)}";
        }

        /// <summary>
        /// Builds a periodic summary message for the current timing snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to describe.</param>
        /// <returns>A formatted periodic summary log message.</returns>
        private static string BuildPeriodicSummaryMessage(TimingSnapshot snapshot)
        {
            return $"[Timing] Periodic summary | run={snapshot.RunElapsedSeconds:0.0}s | sim={snapshot.SimulationTimeSeconds:0.0}s | {snapshot.BuildCompactSummary(4)}";
        }

        private sealed class TimingAggregate
        {
            private readonly string _name;
            private int _count;
            private double _totalMilliseconds;
            private double _maxMilliseconds;

            public TimingAggregate(string name)
            {
                _name = name;
            }

            public void Add(double elapsedMs)
            {
                _count += 1;
                _totalMilliseconds += elapsedMs;
                _maxMilliseconds = Math.Max(_maxMilliseconds, elapsedMs);
            }

            public TimingSectionSnapshot ToSnapshot()
            {
                return new TimingSectionSnapshot(_name, _count, _totalMilliseconds, _maxMilliseconds);
            }
        }
    }

    /// <summary>
    /// Disposable timing scope that records the elapsed time when it is disposed.
    /// </summary>
    public readonly struct TimingScope : IDisposable
    {
        private readonly PacManTimingKeeper _keeper;
        private readonly string _sectionName;
        private readonly long _startTimestamp;
        private readonly bool _enabled;

        /// <summary>
        /// Creates a timing scope bound to a keeper and timestamp.
        /// </summary>
        /// <param name="keeper">The keeper that receives the finished sample.</param>
        /// <param name="sectionName">The section label associated with the sample.</param>
        /// <param name="startTimestamp">The timestamp captured at scope start.</param>
        internal TimingScope(PacManTimingKeeper keeper, string sectionName, long startTimestamp)
        {
            _keeper = keeper;
            _sectionName = sectionName;
            _startTimestamp = startTimestamp;
            _enabled = true;
        }

        /// <summary>
        /// Completes the timing scope and writes the sample to the keeper.
        /// </summary>
        public void Dispose()
        {
            if (!_enabled)
            {
                return;
            }

            _keeper?.EndScope(_sectionName, _startTimestamp);
        }
    }

    /// <summary>
    /// Immutable view of a timing session, including all aggregated sections and summary metadata.
    /// </summary>
    public sealed class TimingSnapshot
    {
        private const string TickTotalSectionName = "Total";

        /// <summary>
        /// Gets an empty snapshot with no collected samples.
        /// </summary>
        public static readonly TimingSnapshot Empty = new TimingSnapshot(null, 0f, 0f, Array.Empty<TimingSectionSnapshot>(), false, 0f, 0.0, 0.0, 0.0);

        /// <summary>
        /// Creates a snapshot from aggregate timing data.
        /// </summary>
        /// <param name="reason">Optional reason or context for the snapshot.</param>
        /// <param name="runElapsedSeconds">Elapsed real time since the session began.</param>
        /// <param name="simulationTimeSeconds">The reported simulation time in seconds.</param>
        /// <param name="sections">The collected timing sections.</param>
        /// <param name="isLive">Whether the snapshot is still live or has been frozen.</param>
        /// <param name="snapshotRealtime">The real time when the snapshot was taken.</param>
        /// <param name="totalTrackedMilliseconds">The total accumulated milliseconds across all sections.</param>
        /// <param name="totalRunMilliseconds">The total elapsed time in milliseconds since the session began.</param>
        /// <param name="totalObservedMilliseconds">The total tick time in milliseconds used as the denominator for percentages.</param>
        public TimingSnapshot(string reason, float runElapsedSeconds, float simulationTimeSeconds, TimingSectionSnapshot[] sections, bool isLive, float snapshotRealtime, double totalTrackedMilliseconds, double totalRunMilliseconds, double totalObservedMilliseconds)
        {
            Reason = reason;
            RunElapsedSeconds = runElapsedSeconds;
            SimulationTimeSeconds = simulationTimeSeconds;
            Sections = sections ?? Array.Empty<TimingSectionSnapshot>();
            IsLive = isLive;
            SnapshotRealtime = snapshotRealtime;
            TotalTrackedMilliseconds = Math.Max(0.0, totalTrackedMilliseconds);
            TotalRunMilliseconds = Math.Max(0.0, totalRunMilliseconds);
            TotalObservedMilliseconds = Math.Max(0.0, totalObservedMilliseconds);
        }

        /// <summary>Gets the optional context string stored with the snapshot.</summary>
        public string Reason { get; }

        /// <summary>Gets the elapsed real time in seconds since the timing session started.</summary>
        public float RunElapsedSeconds { get; }

        /// <summary>Gets the simulation time in seconds associated with the snapshot.</summary>
        public float SimulationTimeSeconds { get; }

        /// <summary>Gets the aggregated timing sections, sorted by total duration descending.</summary>
        public TimingSectionSnapshot[] Sections { get; }

        /// <summary>Gets a value indicating whether the snapshot is still live.</summary>
        public bool IsLive { get; }

        /// <summary>Gets the real-time timestamp when the snapshot was created.</summary>
        public float SnapshotRealtime { get; }

        /// <summary>Gets the total accumulated duration across all tracked sections in milliseconds.</summary>
        public double TotalTrackedMilliseconds { get; }

        /// <summary>Gets the total elapsed run time in milliseconds.</summary>
        public double TotalRunMilliseconds { get; }

        /// <summary>Gets the total tick time in milliseconds used for section and untracked percentages.</summary>
        public double TotalObservedMilliseconds { get; }

        /// <summary>Gets the portion of total tick time that was not attributed to tracked sections.</summary>
        public double UntrackedMilliseconds => Math.Max(0.0, TotalObservedMilliseconds - TotalTrackedMilliseconds);

        /// <summary>
        /// Gets a value indicating whether the snapshot contains at least one timing section.
        /// </summary>
        public bool HasData => Sections != null && Sections.Length > 0;

        /// <summary>
        /// Builds a compact single-line summary of the most significant timing sections.
        /// </summary>
        /// <param name="maxSections">The maximum number of sections to include.</param>
        /// <returns>A formatted summary string.</returns>
        public string BuildCompactSummary(int maxSections)
        {
            var displaySections = GetDisplaySections();
            if (displaySections.Length == 0)
            {
                var emptyState = IsLive ? "live" : "frozen";
                return $"{emptyState}{BuildMetadataSuffix()} | total={TotalObservedMilliseconds:0.0}ms | tracked={TotalTrackedMilliseconds:0.0}ms | untracked={UntrackedMilliseconds:0.0}ms ({GetPercentage(UntrackedMilliseconds):0.0}%) | no samples yet";
            }

            var builder = new StringBuilder();
            var count = Mathf.Clamp(maxSections, 1, displaySections.Length);
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" | ");
                }

                var section = displaySections[i];
                builder.Append(FormatSectionSummary(section));
            }

            return $"{(IsLive ? "live" : "frozen")}{BuildMetadataSuffix()} | total={TotalObservedMilliseconds:0.0}ms | tracked={TotalTrackedMilliseconds:0.0}ms | untracked={UntrackedMilliseconds:0.0}ms ({GetPercentage(UntrackedMilliseconds):0.0}%) | {builder}";
        }

        private string FormatSectionSummary(TimingSectionSnapshot section)
        {
            double share = GetPercentage(section.TotalMilliseconds);

            return new StringBuilder()
                .Append(section.Name)
                .Append(' ')
                .Append(section.TotalMilliseconds.ToString("0.0"))
                .Append("ms (")
                .Append(share.ToString("0.0"))
                .Append("%) x")
                .Append(section.SampleCount)
                .Append(" (max ")
                .Append(section.MaxMilliseconds.ToString("0.0"))
                .Append("ms)")
                .ToString();
        }

        private double GetPercentage(double valueMilliseconds)
        {
            if (TotalObservedMilliseconds <= 0.0)
            {
                return 0.0;
            }

            return (valueMilliseconds / TotalObservedMilliseconds) * 100.0;
        }

        /// <summary>
        /// Builds an ASCII bar plot of the most expensive timing sections.
        /// </summary>
        /// <param name="maxSections">The maximum number of sections to plot.</param>
        /// <param name="barWidth">The width of each rendered bar in characters.</param>
        /// <returns>A multi-line ASCII plot string.</returns>
        public string BuildAsciiBarPlot(int maxSections = 8, int barWidth = 24)
        {
            var displaySections = GetDisplaySections();
            if (displaySections.Length == 0)
            {
                return $"{(IsLive ? "live" : "frozen")}{BuildMetadataSuffix()} | total={TotalObservedMilliseconds:0.0}ms | tracked={TotalTrackedMilliseconds:0.0}ms | untracked={UntrackedMilliseconds:0.0}ms ({GetPercentage(UntrackedMilliseconds):0.0}%) | no samples yet";
            }

            var builder = new StringBuilder();
            var orderedSections = displaySections
                .OrderByDescending(section => section.TotalMilliseconds)
                .ThenBy(section => section.Name)
                .Take(Mathf.Clamp(maxSections, 1, displaySections.Length))
                .ToArray();

            var maxTotal = orderedSections.Max(section => section.TotalMilliseconds);
            builder.AppendLine($"{(IsLive ? "live" : "final")} timing plot{BuildMetadataSuffix()} | total={TotalObservedMilliseconds:0.0}ms | tracked={TotalTrackedMilliseconds:0.0}ms | untracked={UntrackedMilliseconds:0.0}ms ({GetPercentage(UntrackedMilliseconds):0.0}%)");

            foreach (var section in orderedSections)
            {
                var share = GetPercentage(section.TotalMilliseconds);
                var fill = maxTotal <= 0.0
                    ? 0
                    : Mathf.Clamp(Mathf.RoundToInt((float)(section.TotalMilliseconds / maxTotal * barWidth)), 1, barWidth);
                var bar = new string('#', fill).PadRight(barWidth, '.');
                builder.AppendLine(
                    $"{section.Name,-16} [{bar}] {section.TotalMilliseconds:0.00} ms ({share:0.0}%)  avg {section.AverageMilliseconds:0.00}  max {section.MaxMilliseconds:0.00}  n {section.SampleCount}");
            }

            if (UntrackedMilliseconds > 0.0)
            {
                var untrackedFill = maxTotal <= 0.0
                    ? 0
                    : Mathf.Clamp(Mathf.RoundToInt((float)(UntrackedMilliseconds / maxTotal * barWidth)), 1, barWidth);
                var untrackedBar = new string('-', untrackedFill).PadRight(barWidth, '.');
                builder.AppendLine(
                    $"{"Untracked",-16} [{untrackedBar}] {UntrackedMilliseconds:0.00} ms ({GetPercentage(UntrackedMilliseconds):0.0}%)  avg n/a  max n/a  n 0");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Builds a suffix that includes optional snapshot metadata.
        /// </summary>
        /// <returns>A formatted metadata suffix string.</returns>
        private string BuildMetadataSuffix()
        {
            var suffix = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(Reason))
            {
                suffix.Append(" | ").Append(Reason);
            }

            if (SnapshotRealtime > 0f)
            {
                suffix.Append(" | snap=").Append(SnapshotRealtime.ToString("0.0")).Append("s");
            }

            return suffix.ToString();
        }

        private TimingSectionSnapshot[] GetDisplaySections()
        {
            return Sections
                .Where(section => !string.Equals(section.Name, TickTotalSectionName, StringComparison.Ordinal))
                .ToArray();
        }
    }

    /// <summary>
    /// Immutable timing data for a single named section.
    /// </summary>
    public readonly struct TimingSectionSnapshot
    {
        /// <summary>
        /// Creates a timing section snapshot.
        /// </summary>
        /// <param name="name">The section name.</param>
        /// <param name="sampleCount">The number of samples collected.</param>
        /// <param name="totalMilliseconds">The accumulated duration in milliseconds.</param>
        /// <param name="maxMilliseconds">The maximum sample duration in milliseconds.</param>
        public TimingSectionSnapshot(string name, int sampleCount, double totalMilliseconds, double maxMilliseconds)
        {
            Name = name;
            SampleCount = sampleCount;
            TotalMilliseconds = totalMilliseconds;
            MaxMilliseconds = maxMilliseconds;
        }

        /// <summary>Gets the section name.</summary>
        public string Name { get; }

        /// <summary>Gets the number of samples in the section.</summary>
        public int SampleCount { get; }

        /// <summary>Gets the total accumulated duration in milliseconds.</summary>
        public double TotalMilliseconds { get; }

        /// <summary>Gets the longest single sample duration in milliseconds.</summary>
        public double MaxMilliseconds { get; }

        /// <summary>Gets the average sample duration in milliseconds.</summary>
        public double AverageMilliseconds => SampleCount <= 0 ? 0.0 : TotalMilliseconds / SampleCount;
    }
}






