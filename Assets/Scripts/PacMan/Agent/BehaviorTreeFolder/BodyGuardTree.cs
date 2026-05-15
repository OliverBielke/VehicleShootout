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
        public bool enemyLikelyCrossingMyLane;
        public bool safeMiddlePillsAvailable;
        public bool outsideDefensiveZone;
        public bool hasTeamLeader;
        
        public Vector3 teamLeaderPosition;
        public Vector3 powerCapsuleTargetPosition;
        public Vector3 homeTargetPosition;
        public Vector3 enemyPillTargetPosition;
        public Vector3 suspectedIntruderPosition;
        public Vector3 predictedCrossingPoint;
        public Vector3 safeMiddlePillPosition;
        public Vector3 formationPoint;
        public Vector3 dropZonePoint;

        public string debugReason;
    }

    public static class BodyGuardTreeFactory
    {
        public static BehaviorTree<BodyGuardBlackboard> Create()
        {
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
                        ),

                        new SequenceNode<BodyGuardBlackboard>(
                            "Grab Power Capsule Sequence",
                            new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                            {
                                new ConditionNode<BodyGuardBlackboard>(
                                    "shouldGrabPowerCapsule",
                                    bb => bb.shouldGrabPowerCapsule
                                ),
                                new ActionNode<BodyGuardBlackboard>(
                                    "GrabPowerCapsule",
                                    bb => BTDecision.Running(
                                        AgentMode.Attack,
                                        "GrabPowerCapsule",
                                        hasTarget: true,
                                        targetPosition: bb.powerCapsuleTargetPosition
                                    )
                                )
                            }
                        ),

                        new SequenceNode<BodyGuardBlackboard>(
                            "Return Home Sequence",
                            new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                            {
                                new ConditionNode<BodyGuardBlackboard>(
                                    "shouldReturnHome",
                                    bb => bb.shouldReturnHome
                                ),
                                new ActionNode<BodyGuardBlackboard>(
                                    "ReturnHome",
                                    bb => BTDecision.Running(
                                        AgentMode.ReturnHome,
                                        "ReturnHome",
                                        hasTarget: true,
                                        targetPosition: bb.homeTargetPosition
                                    )
                                )
                            }
                        ),

                        new SequenceNode<BodyGuardBlackboard>(
                            "Loot While Powered Sequence",
                            new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                            {
                                new ConditionNode<BodyGuardBlackboard>(
                                    "shouldLootWhilePowered",
                                    bb => bb.shouldLootWhilePowered
                                ),
                                new ActionNode<BodyGuardBlackboard>(
                                    "CollectEnemyPills",
                                    bb => BTDecision.Running(
                                        AgentMode.Attack,
                                        "CollectEnemyPills",
                                        hasTarget: true,
                                        targetPosition: bb.enemyPillTargetPosition
                                    )
                                )
                            }
                        ),

                        new SequenceNode<BodyGuardBlackboard>(
                            "Intercept Intruder Sequence",
                            new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                            {
                                new ConditionNode<BodyGuardBlackboard>(
                                    "enemyPacmanIntruderSuspected",
                                    bb => bb.enemyPacmanIntruderSuspected
                                ),
                                new ActionNode<BodyGuardBlackboard>(
                                    "InterceptIntruder",
                                    bb => BTDecision.Running(
                                        AgentMode.Defend,
                                        "InterceptIntruder",
                                        hasTarget: true,
                                        targetPosition: bb.suspectedIntruderPosition
                                    )
                                )
                            }
                        ),

                        new SequenceNode<BodyGuardBlackboard>(
                            "Block Crossing Sequence",
                            new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                            {
                                new ConditionNode<BodyGuardBlackboard>(
                                    "enemyLikelyCrossingMyLane",
                                    bb => bb.enemyLikelyCrossingMyLane
                                ),
                                new ActionNode<BodyGuardBlackboard>(
                                    "BlockCrossing",
                                    bb => BTDecision.Running(
                                        AgentMode.Defend,
                                        "BlockCrossing",
                                        hasTarget: true,
                                        targetPosition: bb.predictedCrossingPoint
                                    )
                                )
                            }
                        ),

                        new SequenceNode<BodyGuardBlackboard>(
                            "Collect Safe Middle Pills Sequence",
                            new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                            {
                                new ConditionNode<BodyGuardBlackboard>(
                                    "safeMiddlePillsAvailable",
                                    bb => bb.safeMiddlePillsAvailable
                                ),
                                new ActionNode<BodyGuardBlackboard>(
                                    "CollectSafeMiddlePills",
                                    bb => BTDecision.Running(
                                        AgentMode.Defend,
                                        "CollectSafeMiddlePills",
                                        hasTarget: true,
                                        targetPosition: bb.safeMiddlePillPosition
                                    )
                                )
                            }
                        ),

                        new SequenceNode<BodyGuardBlackboard>(
                            "Move To Formation Sequence",
                            new System.Collections.Generic.List<BTNode<BodyGuardBlackboard>>
                            {
                                new ConditionNode<BodyGuardBlackboard>(
                                    "outsideDefensiveZone",
                                    bb => bb.outsideDefensiveZone
                                ),
                                new ActionNode<BodyGuardBlackboard>(
                                    "MoveToFormation",
                                    bb => BTDecision.Running(
                                        AgentMode.Defend,
                                        "MoveToFormation",
                                        hasTarget: true,
                                        targetPosition: bb.formationPoint
                                    )
                                )
                            }
                        ),

                        new ActionNode<BodyGuardBlackboard>(
                            "HoldDropZone",
                            bb => BTDecision.Running(
                                AgentMode.Defend,
                                "HoldDropZone",
                                hasTarget: true,
                                targetPosition: bb.dropZonePoint
                            )
                        )
                    }
                );

            return new BehaviorTree<BodyGuardBlackboard>(root);
        }
    }
}
