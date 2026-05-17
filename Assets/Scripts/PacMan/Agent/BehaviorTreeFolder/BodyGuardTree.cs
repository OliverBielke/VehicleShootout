using UnityEngine;

namespace PacMan.Agent.BehaviorTreeFolder
{
    [System.Serializable]
    public class BodyGuardBlackboard
    {
        public bool shouldGrabPowerCapsule;
        public bool shouldLootWhilePowered;
        public bool shouldReturnHome;
        public bool enemyPacmanIntruderSuspected;
        public bool safeMiddlePillsAvailable;
        public bool outsideDefensiveZone;
        public bool hasTeamLeader;
        
        public Vector3 teamLeaderPosition;
        public Vector3 powerCapsuleTargetPosition;
        public Vector3 homeTargetPosition;
        public Vector3 enemyPillTargetPosition;
        public Vector3 suspectedIntruderPosition;
        public Vector3 safeMiddlePillPosition;
        public Vector3 formationPoint;
        public Vector3 dropZonePoint;

        public string debugReason;
    }

    public static class BodyGuardTreeFactory
    {
        public static BehaviorTree<BodyGuardBlackboard> Create()
        {
            // Only keep the "Group Up With Team" behavior for bodyguards per request.
            BTNode<BodyGuardBlackboard> root =
                new SelectorNode<BodyGuardBlackboard>(
                    "Body Guard Selector",
                    new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                    {
                        new SequenceNode<BodyGuardBlackboard>(
                            "Group Up With Team Sequence",
                            new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                            {
                                new ConditionNode<BodyGuardBlackboard>(
                                    "Has Leader",
                                    bb => bb.hasTeamLeader
                                ),
                                new ActionNode<BodyGuardBlackboard>(
                                    "MoveToLeader",
                                    bb => BTDecision.Running(
                                        AgentMode.Defend,
                                        "MoveToLeader",
                                        hasTarget: true,
                                        targetPosition: bb.teamLeaderPosition
                                    )
                                )
                            }
                        )
                    }
                );

            return new BehaviorTree<BodyGuardBlackboard>(root);
        }
    }
}
