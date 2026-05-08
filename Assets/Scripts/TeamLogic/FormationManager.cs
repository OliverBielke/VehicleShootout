using System;
using System.Collections.Generic;
using System.Linq;
using PacMan.Agent;
using PacMan.Agent.Debugging;
using PacMan.Local;
using UnityEngine;
using UnityEngine.PlayerLoop;


public class FormationInformation
{
    public Vector3 Center =  Vector3.zero;
    public Vector3 Direction = Vector3.zero;
    public float StepSize = .5f;
    public float MedianHP = 100f;
    public List<PacManAgentManager> Agents = new List<PacManAgentManager>();
    public List<Vector3> Positions = new List<Vector3>();
    public IShape Shape = IShape.Phalanx;
    public PacManAIDebugBT leaderAI;
}

public enum IShape
{
    NoShape,
    Phalanx,
    Line
}

public class FormationManager : MonoBehaviour
{
    public static FormationManager Instance;
    public Dictionary<PacManAgentManager,FormationInformation> Groups = new Dictionary<PacManAgentManager, FormationInformation>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }
    
    public void RegisterGroup(PacManAgentManager leader, List<PacManAgentManager> members, IShape Shape = IShape.Phalanx)
    {
        Groups[leader] = new FormationInformation()
        {
            Center = leader.transform.position, 
            StepSize = 1f,
            Direction = leader.transform.forward,
            MedianHP = members.Select(a => a.GetHealth()).OrderBy(hp => hp).ElementAt(members.Count / 2),
            Agents = members,
            Shape = Shape,
            leaderAI = leader.GetComponent<PacManAIDebugBT>()
        };
    }
    public void DeRegisterGroup(PacManAgentManager leader)
    {
        Groups.Remove(leader);
    }

    public void SwapLeader(PacManAgentManager oldLeader, PacManAgentManager newLeader)
    {
        if (Groups.TryGetValue(oldLeader, out FormationInformation formation))
        {
            if (formation.Agents.Contains(newLeader))
            {
                Groups[newLeader] = formation;
                Groups.Remove(oldLeader);
                formation.leaderAI = newLeader.GetComponent<PacManAIDebugBT>();
                formation.Center =  newLeader.transform.position;
                formation.Direction = newLeader.transform.forward;
            }
            else
            {
                Debug.Log("New leader is not a member of the group! Returning...");
                return;
            }
        }
        else
        {
            Debug.Log("Old leaders group cannot be found! Returning...");
        }
    }

    private void FixedUpdate()
    {
        UpdatePositionAssignments();
    }
    
    private void UpdatePositionAssignments()
    {
        if (Groups == null || Groups.Count == 0)
        {
            Debug.Log("No groups registered!");
            return;
        }

        foreach (KeyValuePair<PacManAgentManager, FormationInformation> pair in Groups)
        {
            FormationInformation formation = pair.Value;
            PacManAgentManager leader = pair.Key;
            formation.Center = leader.transform.position;
            
            Vector3 closestEnemyDirection = formation.leaderAI.AgentManager.GetVisibleEnemyAgents().Select(e => e.transform.position - formation.Center)
                .OrderBy(dir => dir.magnitude)
                .FirstOrDefault();
            
            formation.Direction = closestEnemyDirection;
            
            if (formation.Agents == null || formation.Agents.Count == 0)
            {
                Debug.Log("No agents in group of leader " + leader.name);
                continue;
            }
            
            formation.Shape = getBestShape(formation);
            
            var assignments = formation.Shape switch
            {
                IShape.Phalanx => GetPhalanxPositions(formation),
                IShape.Line => GetLinePositions(formation),
                IShape.NoShape => GetNoShapePositions(formation),
                _ => null
            };

            if (assignments == null || assignments.Count == 0)
                continue;
            
            foreach (KeyValuePair<PacManAgentManager, Vector3> assignment in assignments)
            {
                PacManAIDebugBT agentAI = assignment.Key.GetComponent<PacManAIDebugBT>();
                if (agentAI != null)
                {
                    agentAI.FormationAnchor = assignment.Value;
                }
            }
        }
    }

    private IShape getBestShape(FormationInformation formation)
    {
        float minSpace = .75f;
        if (Physics.SphereCast(formation.Center + Vector3.up*3f, minSpace, Vector3.down, out RaycastHit hit, 2f, 
                layerMask: LayerMask.GetMask("Obstacle")))
        {
            if (DebugManager.Instance != null && DebugManager.Instance.visualizeGroups)
            {
                Debug.DrawLine(formation.Center, hit.point, Color.red, 1f);
            }
            return IShape.NoShape;
        }
        else
        {
            return IShape.Phalanx;
        }
    }
    private Dictionary<PacManAgentManager, Vector3> GetNoShapePositions(FormationInformation formation)
    {
        Dictionary<PacManAgentManager, Vector3> positionAssignments = new Dictionary<PacManAgentManager, Vector3>();
        formation.Positions = new List<Vector3>();
        
        for (int i = 0; i < formation.Agents.Count ; i++)
        {
            formation.Positions.Add(formation.leaderAI.LastTargetPosition);
            positionAssignments[formation.Agents[i]] = formation.Positions[i];
        }
        
        return positionAssignments;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Dictionary<PacManAgentManager, Vector3> GetPhalanxPositions(FormationInformation formation)
    {
        Dictionary<PacManAgentManager, Vector3> positionAssignments = new Dictionary<PacManAgentManager, Vector3>();
        float MedianHP = formation.Agents.Select(a => a.GetHealth()).OrderBy(hp => hp).ElementAt(formation.Agents.Count / 2);
        Vector3 leftParallel = Vector3.Cross(formation.Direction, Vector3.up).normalized;
        formation.Positions = new List<Vector3>();
        
        int rel_i;
        for (int i = 0; i < formation.Agents.Count ; i++)
        {
            rel_i = i - formation.Agents.Count / 2;
            formation.Positions.Add(formation.Center + rel_i*formation.StepSize*leftParallel);
        }
        
        Vector3 rowAdjustment;
        formation.Agents = formation.Agents.OrderBy( a => Vector3.Dot(a.transform.position-formation.Center, leftParallel)).ToList(); 
        formation.Positions = formation.Positions.OrderBy( pos => Vector3.Dot(pos-formation.Center, leftParallel)).ToList(); 
        
        for (int i = 0; i < formation.Agents.Count ; i++)
        {
            rowAdjustment = (formation.Agents[i].GetHealth()<MedianHP? -1f:1f)*formation.StepSize/1.75f * formation.Direction.normalized;
            formation.Positions[i] += rowAdjustment;
            positionAssignments[formation.Agents[i]] = formation.Positions[i];
        }
        return positionAssignments;
    }
    
    private Dictionary<PacManAgentManager, Vector3> GetLinePositions(FormationInformation formation)
    {
        Dictionary<PacManAgentManager, Vector3> positionAssignments = new Dictionary<PacManAgentManager, Vector3>();
        float MedianHP = formation.Agents.Select(a => a.GetHealth()).OrderBy(hp => hp).ElementAt(formation.Agents.Count / 2);
        //Vector3 leftParallel = Vector3.Cross(formation.Direction, Vector3.up).normalized;
        formation.Positions = new List<Vector3>();
        
        int rel_i;
        for (int i = 0; i < formation.Agents.Count ; i++)
        {
            rel_i = i;
            formation.Positions.Add(formation.Center + rel_i*formation.StepSize*formation.Direction.normalized);
        }
        
        Vector3 rowAdjustment;
        formation.Agents = formation.Agents.OrderBy( a => Vector3.Dot(a.transform.position-formation.Center, formation.Direction)).ToList(); 
        formation.Positions = formation.Positions.OrderBy( pos => Vector3.Dot(pos-formation.Center, formation.Direction)).ToList(); 
        
        for (int i = 0; i < formation.Agents.Count ; i++)
        {
            positionAssignments[formation.Agents[i]] = formation.Positions[i];
        }
        return positionAssignments;
    }
    
    
    // Visualizations
    private void OnDrawGizmos()
    {
        if (DebugManager.Instance == null || !DebugManager.Instance.visualizeGroups)
            return;

        foreach (KeyValuePair<PacManAgentManager, FormationInformation> pair in Groups)
        {
            if (pair.Value.Positions == null)
                continue;
                
            foreach (Vector3 pos in pair.Value.Positions)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(pos, 0.25f);
            }
        }
    }

    private Vector3 MeanPosition(List<Vector3> positions)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 pos in positions)
            sum += pos;
        return sum / positions.Count;
    }
}
