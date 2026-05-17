using System.Collections.Generic;
using System.Linq;
using PacMan.Agent.Map;
using PacMan.Local;
using UnityEngine;

namespace PacMan.Agent.RoleAssignment
{
    public class BodyGuardManager
    {
        private readonly Dictionary<Team, HashSet<int>> _latchedIntrudersByTeam = new();

        public class Assignment
        {
            public int EnemyServerIndex;
            public Vector3 TargetPosition;
            public bool IsVisible;
            public string Reason;
        }

        private readonly RoleAssigner _roleAssigner;

        public BodyGuardManager(RoleAssigner roleAssigner)
        {
            _roleAssigner = roleAssigner;
        }

        public Assignment GetAssignment(PacManAIDebugBT requester)
        {
            if (requester == null)
                return null;

            Team team = TeamAssignmentUtil.CheckTeam(requester.gameObject);
            if (team == Team.Undefined)
                return null;

            var bodyGuards = _roleAssigner
                .GetRegisteredAgentsForTeam(team)
                .Where(agent => agent != null && agent.AssignedRole == StaticRole.BodyGuard)
                .ToList();

            if (bodyGuards.Count == 0)
                return null;

            if (!_latchedIntrudersByTeam.TryGetValue(team, out var latchedIntruders))
            {
                latchedIntruders = new HashSet<int>();
                _latchedIntrudersByTeam[team] = latchedIntruders;
            }

            var trackedIntruders = new Dictionary<int, Assignment>();
            var intrudersThisTick = new HashSet<int>();

            foreach (var bodyGuard in bodyGuards)
            {
                var trackedEnemies = bodyGuard.GetTrackedEnemies();
                if (trackedEnemies == null)
                    continue;

                foreach (var enemy in trackedEnemies)
                {
                    if (enemy == null || !enemy.HasPosition)
                        continue;

                    bool isLatched = latchedIntruders.Contains(enemy.ServerIndex);
                    bool entersBufferedZone = !enemy.IsGhost && IsIntrudingIntoTeamTerritory(team, enemy.Position, useBuffer: true);
                    bool remainsAcrossMidline = !enemy.IsGhost && IsIntrudingIntoTeamTerritory(team, enemy.Position, useBuffer: false);
                    bool shouldTrack = entersBufferedZone || (isLatched && remainsAcrossMidline);

                    if (!shouldTrack)
                        continue;

                    intrudersThisTick.Add(enemy.ServerIndex);

                    if (!trackedIntruders.TryGetValue(enemy.ServerIndex, out var current))
                    {
                        trackedIntruders[enemy.ServerIndex] = new Assignment
                        {
                            EnemyServerIndex = enemy.ServerIndex,
                            TargetPosition = enemy.Position,
                            IsVisible = enemy.IsVisible,
                            Reason = GetAssignmentReason(enemy.IsVisible, isLatched && remainsAcrossMidline && !entersBufferedZone)
                        };
                        continue;
                    }

                    // Prefer exact visible positions when available, otherwise keep the latest estimate.
                    if (!current.IsVisible || enemy.IsVisible)
                    {
                        current.TargetPosition = enemy.Position;
                        current.IsVisible = enemy.IsVisible;
                        current.Reason = GetAssignmentReason(enemy.IsVisible, isLatched && remainsAcrossMidline && !entersBufferedZone);
                    }
                }
            }

            latchedIntruders.Clear();
            foreach (int intruderId in intrudersThisTick)
                latchedIntruders.Add(intruderId);

            if (trackedIntruders.Count == 0)
                return null;

            var bestAssignments = new Dictionary<PacManAIDebugBT, Assignment>();
            var intruderCandidates = trackedIntruders.Values
                .SelectMany(intruder => bodyGuards.Select(bodyGuard => new
                {
                    BodyGuard = bodyGuard,
                    Intruder = intruder,
                    Score = ScoreIntercept(bodyGuard, intruder.TargetPosition)
                }))
                .OrderBy(candidate => candidate.Score)
                .ToList();

            var usedBodyGuards = new HashSet<PacManAIDebugBT>();
            var usedIntruders = new HashSet<int>();

            foreach (var candidate in intruderCandidates)
            {
                if (usedBodyGuards.Contains(candidate.BodyGuard) || usedIntruders.Contains(candidate.Intruder.EnemyServerIndex))
                    continue;

                usedBodyGuards.Add(candidate.BodyGuard);
                usedIntruders.Add(candidate.Intruder.EnemyServerIndex);
                bestAssignments[candidate.BodyGuard] = candidate.Intruder;
            }

            return bestAssignments.TryGetValue(requester, out var assignment) ? assignment : null;
        }

        private static float ScoreIntercept(PacManAIDebugBT bodyGuard, Vector3 intruderPosition)
        {
            Vector3 bodyGuardPos = bodyGuard.transform.localPosition;
            float distanceScore = (bodyGuardPos - intruderPosition).sqrMagnitude;

            if (!bodyGuard.HasDefenseAnchor)
                return distanceScore;

            float laneDelta = Mathf.Abs(bodyGuard.DefenseAnchor.z - intruderPosition.z);
            return distanceScore + laneDelta * laneDelta * 0.35f;
        }

        private static string GetAssignmentReason(bool isVisible, bool isLatchedRetreat)
        {
            if (isLatchedRetreat)
                return isVisible ? "Following retreating intruder" : "Tracking retreating intruder";

            return isVisible ? "Assigned visible intruder" : "Assigned particle-filter intruder";
        }

        private bool IsIntrudingIntoTeamTerritory(Team defendingTeam, Vector3 enemyPosition, bool useBuffer)
        {
            float midlineBuffer = useBuffer && _roleAssigner != null ? _roleAssigner.DefenderIntrusionMidlineBuffer : 0f;
            float midXLocal = _roleAssigner != null ? _roleAssigner.MidXLocal : 0f;

            if (defendingTeam == Team.Blue)
                return enemyPosition.x < (midXLocal - midlineBuffer);

            if (defendingTeam == Team.Red)
                return enemyPosition.x > (midXLocal + midlineBuffer);

            return false;
        }
    }
}

