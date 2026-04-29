using System;
using System.Collections.Generic;
using System.Linq;
using PacMan.Agent;
using PacMan.Local;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class PhalanxShape : MonoBehaviour
{
    public float widthStep, heightStep;
    public Vector3 center = Vector3.zero;
    public Vector3 direction = Vector3.forward; // Direction the perpendicular to line
    public float MedianHP { get; private set;}
    public List<PacManAgentManager> Agents = new List<PacManAgentManager>();
    public bool IsBlue = true;
    
    public List<Vector3> Positions {get; private set;} = new List<Vector3>();
    [SerializeField] private GameObject gameManagerObject;
    private void RegisterAgents()
    {
        if (Agents == null)
            Agents = new List<PacManAgentManager>();
        foreach (PacManAgentManager agent in gameManagerObject.GetComponentsInChildren<PacManAgentManager>())
        {
            if (agent.CompareTag("Blue") == IsBlue) // True-True for blue, False-False for Red
            {
                Agents.Add(agent);
            }
        }
    }

    private void FixedUpdate()
    {
        UpdateLinePositions();
        UpdatePositionAssignments();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void UpdateLinePositions()
    {
        if (Agents == null || Agents.Count == 0)
            RegisterAgents();
        
        MedianHP = Agents.Select(a => a.GetHealth()).OrderBy(hp => hp).ElementAt(Agents.Count / 2);
        Vector3 leftParallel = Vector3.Cross(direction, Vector3.up).normalized;
        
        int rel_i;
        Positions.Clear();
        for (int i = 0; i < Agents.Count ; i++)
        {
            rel_i = i - Agents.Count / 2;
            Positions.Add(center + rel_i*widthStep*leftParallel);
        }
    }
    
    private void UpdatePositionAssignments()
    {
        if (Agents == null || Agents.Count != Positions.Count)
            return;
        
        Vector3 leftParallel = Vector3.Cross(direction, Vector3.up).normalized;
        MedianHP = Agents.Select(a => a.GetHealth()).OrderBy(hp => hp).ElementAt(Agents.Count / 2);
        Vector3 rowAdjustment;
        
        Agents = Agents.OrderBy( a => Vector3.Dot(a.transform.position-center, leftParallel)).ToList(); // Sort by health, lowest to highest
        Positions = Positions.OrderBy( pos => Vector3.Dot(pos-center, leftParallel)).ToList(); // Sort by health, lowest to highest
        
        for (int i = 0; i < Agents.Count ; i++)
        {
            rowAdjustment = (Agents[i].GetHealth()<MedianHP? -1f:1f)*heightStep/2 * direction;
            PacManAI agentAI = Agents[i].GetComponent<PacManAI>();
            if (agentAI != null)
            {
                Vector3 closestPosition = Positions[i] + rowAdjustment;
                agentAI.FormationPosition = closestPosition;
            }
        }
    }


    // Visualizations
    public bool VisualizeShape = true;
    private void OnDrawGizmos()
    {
        foreach (Vector3 pos in Positions)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pos, 0.1f);
        }
    }
}
