using UnityEngine;

namespace PacMan.Agent.BehaviorTreeFolder
{
    [System.Serializable]
    public class DefenderBlackboard
    {
        public bool shouldGroupUp;
        public bool shouldGrabPowerCapsule;
        public bool shouldLootWhilePowered;
        public bool shouldReturnHome;
        /// <summary>True when the defender should hold a square near the border because nearby friendly support outmatches visible/tracked enemies.</summary>
        public bool borderAdvantageSquareAvailable;
        public bool enemyPacmanIntruderSuspected;
        public bool safeMiddlePillsAvailable;
        public bool outsideDefensiveZone;
        public bool hasTeamLeader;
        
        public Vector3 teamLeaderPosition;
        public Vector3 homeTargetPosition;
        public Vector3 enemyPillTargetPosition;
        /// <summary>Target square on our side of the border that offers a health advantage against nearby enemies.</summary>
        public Vector3 borderAdvantageSquarePosition;
        public Vector3 suspectedIntruderPosition;
        public Vector3 safeMiddlePillPosition;
        public Vector3 formationPoint;
        public Vector3 dropZonePoint;
        public Vector3 regroupPoint;
        public string debugReason;
    }

    public static class DefenderTreeFactory
    {
        public static BehaviorTree<DefenderBlackboard> Create()
        {
            BTNode<DefenderBlackboard> root =
                new SelectorNode<DefenderBlackboard>(
                    "Defender Selector",
                    new System.Collections.Generic.List<BTNode<DefenderBlackboard>>
                    {
                        new SequenceNode<DefenderBlackboard>(
                            "Group up with team sequence",
                            new System.Collections.Generic.List<BTNode<DefenderBlackboard>>
                            {
                                new ConditionNode<DefenderBlackboard>(
                                    "ShouldRegroup",
                                    bb => bb.shouldGroupUp
                                ),
                                new ActionNode<DefenderBlackboard>(
                                    "GroupUp",
                                    bb => BTDecision.Running(
                                        AgentMode.Defend,
                                        "GroupUp",
                                        hasTarget: true,
                                        targetPosition: bb.regroupPoint
                                    )
                                )
                            }
                        ),
                        
                        new SequenceNode<DefenderBlackboard>(
                            "Collect Safe Middle Pills Sequence",
                            new System.Collections.Generic.List<BTNode<DefenderBlackboard>>
                            {
                                new ConditionNode<DefenderBlackboard>(
                                    "safeMiddlePillsAvailable",
                                    bb => bb.safeMiddlePillsAvailable
                                ),
                                new ActionNode<DefenderBlackboard>(
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
                        
                        new SequenceNode<DefenderBlackboard>(
                            "Return Home Sequence",
                            new System.Collections.Generic.List<BTNode<DefenderBlackboard>>
                            {
                                new ConditionNode<DefenderBlackboard>(
                                    "shouldReturnHome",
                                    bb => bb.shouldReturnHome
                                ),
                                new ActionNode<DefenderBlackboard>(
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


                        // Hold a nearby border square when our defenders/bodyguards have the health advantage.
                        new SequenceNode<DefenderBlackboard>(
                            "Border Advantage Square Sequence",
                            new System.Collections.Generic.List<BTNode<DefenderBlackboard>>
                            {
                                new ConditionNode<DefenderBlackboard>(
                                    "borderAdvantageSquareAvailable",
                                    bb => bb.borderAdvantageSquareAvailable
                                ),
                                new ActionNode<DefenderBlackboard>(
                                    "MoveToBorderAdvantageSquare",
                                    bb => BTDecision.Running(
                                        AgentMode.Defend,
                                        "MoveToBorderAdvantageSquare",
                                        hasTarget: true,
                                        targetPosition: bb.borderAdvantageSquarePosition
                                    )
                                )
                            }
                        ),

                        new SequenceNode<DefenderBlackboard>(
                            "Move To Formation Sequence",
                            new System.Collections.Generic.List<BTNode<DefenderBlackboard>>
                            {
                                new ConditionNode<DefenderBlackboard>(
                                    "outsideDefensiveZone",
                                    bb => bb.outsideDefensiveZone
                                ),
                                new ActionNode<DefenderBlackboard>(
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

                        new ActionNode<DefenderBlackboard>(
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

            return new BehaviorTree<DefenderBlackboard>(root);
        }
    }
}
