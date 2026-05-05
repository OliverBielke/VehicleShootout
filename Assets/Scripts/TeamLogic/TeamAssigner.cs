
using System.Collections.Generic;
using System.Linq;
using PacMan;
using PacMan.Agent;
using PacMan.Local;
using UnityEngine;

public class TeamAssigner : MonoBehaviour
{
    public List<PacManAgentManager> RedAgents { get; private set; } = new List<PacManAgentManager>();
    public List<PacManAgentManager> BlueAgents { get; private set; } = new List<PacManAgentManager>();

    public Dictionary<PacManAgentManager, List<PacManAgentManager>> MembersByLeaderRed { get; private set; } =
        new Dictionary<PacManAgentManager, List<PacManAgentManager>>();

    public Dictionary<PacManAgentManager, List<PacManAgentManager>> MembersByLeaderBlue { get; private set; } =
        new Dictionary<PacManAgentManager, List<PacManAgentManager>>();

    public Dictionary<PacManAgentManager, PacManAgentManager> LeaderByMemberRed { get; private set; } =
        new Dictionary<PacManAgentManager, PacManAgentManager>();

    public Dictionary<PacManAgentManager, PacManAgentManager> LeaderByMemberBlue { get; private set; } =
        new Dictionary<PacManAgentManager, PacManAgentManager>();
    
    public static TeamAssigner Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    [SerializeField] private int staticGroupSize = 2;
    [SerializeField] private int TeamUpdatePeriod = 300;
    private int currentUpdateFrame = 0;
    private void FixedUpdate()
    {
        if (currentUpdateFrame % TeamUpdatePeriod == 0)
        {
            Debug.Log("Updated Team/Group assignments!");
            if (RedAgents.Count > 0)
                CreateGroupsStaticDuos(false);
            if (BlueAgents.Count > 0)
                CreateGroupsStaticDuos(true);
        }
        
        currentUpdateFrame++;
    }
    
    public void RegisterAgent(PacManAgentManager agent)
    {
        if (agent.CompareTag("Blue"))
            BlueAgents.Add(agent);
        else
            RedAgents.Add(agent);
    }

    public void SwapTeamLeader(PacManAgentManager deadLeader)
    {
        bool isBlue = deadLeader.CompareTag("Blue");
        var teams = isBlue ? MembersByLeaderBlue : MembersByLeaderRed;
        var leaders = isBlue ? LeaderByMemberBlue :  LeaderByMemberRed;
        if (teams.TryGetValue(deadLeader, out List<PacManAgentManager> members))
        {
            if (members.Count == 0)
                return;
            PacManAgentManager newLeader = members[0];
            teams.Remove(deadLeader);
            members.Add(deadLeader);
            teams[newLeader] = members;
            foreach (var agent in members)
            {
                leaders[agent] = newLeader;
            }
            members.Remove(newLeader); // The leader is counted as "member" of its own team
        }
    }
    
    /// <summary>
    /// Returns true if member has leader, and outs the leader in this case. False if it has not yet had a leader assigned,
    /// or is itself the leader. 
    /// </summary>
    /// <param name="agent"></param>
    /// <returns></returns>
    public bool TryGetLeader(PacManAgentManager agent, out PacManAgentManager leader)
    {
        bool isBlue = TeamAssignmentUtil.CheckTeam(agent.gameObject) == Team.Blue;
        var leaderDict = isBlue ? LeaderByMemberBlue : LeaderByMemberRed;
        if (leaderDict.TryGetValue(agent, out var groupLeader))
        {
            leader = groupLeader;
            if (leader == agent)
                return false;
            return true;
        }
        else
        {
            leader = null;
            Debug.LogWarning("Could not find leader for agent!");
            return false;
        }
    }


    private void ClearAssignments(bool isBlue)
    {
        if (isBlue)
        {
            MembersByLeaderBlue.Clear();
            LeaderByMemberBlue.Clear();
        }
        else
        {
            MembersByLeaderRed.Clear();
            LeaderByMemberRed.Clear();
        }
    }
    
    /// <summary>
    /// Use this either when first craeting groups, or reseting the match. Use GetGroupLeader Otherwise!
    /// </summary>
    /// <param name="isBlueTeam"></param>
    /// <returns></returns>
    private void CreateGroupsStaticDuos(bool isBlueTeam)
    {
        ClearAssignments(isBlueTeam);
        List<PacManAgentManager> agents = isBlueTeam ? BlueAgents : RedAgents;
        Dictionary<PacManAgentManager, List<PacManAgentManager>> membersByLeader =
            isBlueTeam ? MembersByLeaderBlue : MembersByLeaderRed;
        Dictionary<PacManAgentManager, PacManAgentManager> leaderByMember =
            isBlueTeam ? LeaderByMemberBlue : LeaderByMemberRed;

        //agents = agents.OrderBy(a => -1 * getLeaderCapacity(a)).ToList();
        int totalCapacity = 0;
        //List<PacManAgentManager> newLeaders = new List<PacManAgentManager>();
        int firstMemberIndex = -1;

        for (int i = 0; i < agents.Count; i++)
        {
            if (totalCapacity >= agents.Count - i)
                break;

            totalCapacity += staticGroupSize-1;
            //newLeaders.Add(agents[i]);
            membersByLeader[agents[i]] = new List<PacManAgentManager>();
            firstMemberIndex = i + 1;
        }

        for (int i = firstMemberIndex; i < agents.Count; i++)
        {
            PacManAgentManager member = agents[i];
            PacManAgentManager closestLeader = null;
            float closestDistance = float.MaxValue;
            for (int j = 0; j < firstMemberIndex; j++)
            {
                PacManAgentManager leader = agents[j];
                float newDist = Vector3.Distance(member.transform.position, leader.transform.position);

                if (newDist < closestDistance && (membersByLeader[leader].Count+.01f) < staticGroupSize-1)
                {
                    closestDistance = newDist;
                    closestLeader = leader;
                }
            }

            if (closestLeader == null)
            {
                Debug.LogWarning("Could not find leader for agent!");
            }
            if (membersByLeader.TryGetValue(closestLeader, out var members))
            {
                membersByLeader.Remove(member);
                members.Add(member);
                leaderByMember[member] = closestLeader;
            }
            else
            {
                Debug.LogWarning("Trying to assign member to non-registered leader!");
            }
        }
    }
    
    
    
    /// <summary>
    /// Use this either when first craeting groups, or reseting the match. Use GetGroupLeader Otherwise!
    /// </summary>
    /// <param name="isBlueTeam"></param>
    /// <returns></returns>
    private void CreateGroupsDynamical(bool isBlueTeam)
    {
        ClearAssignments(isBlueTeam);
        List<PacManAgentManager> agents = isBlueTeam ? BlueAgents : RedAgents;
        Dictionary<PacManAgentManager, List<PacManAgentManager>> membersByLeader =
            isBlueTeam ? MembersByLeaderBlue : MembersByLeaderRed;
        Dictionary<PacManAgentManager, PacManAgentManager> leaderByMember =
            isBlueTeam ? LeaderByMemberBlue : LeaderByMemberRed;

        agents = agents.OrderBy(a => -1 * getLeaderCapacity(a)).ToList();
        int totalCapacity = 0;
        //List<PacManAgentManager> newLeaders = new List<PacManAgentManager>();
        int firstMemberIndex = -1;

        for (int i = 0; i < agents.Count; i++)
        {
            if (totalCapacity >= agents.Count - i)
                break;

            totalCapacity += getLeaderCapacity(agents[i]);
            //newLeaders.Add(agents[i]);
            membersByLeader[agents[i]] = new List<PacManAgentManager>();
            firstMemberIndex = i + 1;
        }

        for (int i = firstMemberIndex; i < agents.Count; i++)
        {
            PacManAgentManager member = agents[i];
            PacManAgentManager closestLeader = null;
            float closestDistance = float.MaxValue;
            for (int j = 0; j < firstMemberIndex; j++)
            {
                PacManAgentManager leader = agents[j];
                float newDist = Vector3.Distance(member.transform.position, leader.transform.position);

                if (newDist < closestDistance && (membersByLeader[leader].Count+.01f) < getLeaderCapacity(leader))
                {
                    closestDistance = newDist;
                    closestLeader = leader;
                }
            }

            if (membersByLeader.TryGetValue(closestLeader, out var members))
            {
                membersByLeader.Remove(member);
                members.Add(member);
                leaderByMember[member] = closestLeader;
            }
            else
            {
                Debug.LogWarning("Trying to assign member to non-registered leader!");
            }
        }
    }
    private HashSet<PacManAgentManager> takenMembers = new HashSet<PacManAgentManager>();
    /// <summary>
    /// Gets the amount of friendly agents within a certain radius of the agent 
    /// </summary>
    /// <param name="agent"></param>
    /// <returns></returns>
    private int getLeaderCapacity(PacManAgentManager agent)
    {
        int closeFriendlies = 0;
        RaycastHit[] hits = Physics.SphereCastAll(agent.transform.position + Vector3.up * 12f, 12f, Vector3.down, 12f,
            layerMask: LayerMask.GetMask("Agent"));
        foreach (RaycastHit hit in hits)
        {
            PacManAgentManager hitAgent = hit.collider.GetComponent<PacManAgentManager>();
            if (takenMembers.Contains(hitAgent))
                continue;
            
            takenMembers.Add(hitAgent);
            closeFriendlies++;
        }
        
        return closeFriendlies;
    }


    [SerializeField] private bool _visualizeGroups = false;

    private void OnDrawGizmos()
    {
        if (!_visualizeGroups)
            return;

        DrawGroupGizmos(MembersByLeaderRed, 0.0f, 1f, 1f); // warm colors for red team
        DrawGroupGizmos(MembersByLeaderBlue, 0.55f, 1f, 1f); // cool colors for blue team
    }

    private void DrawGroupGizmos(
        Dictionary<PacManAgentManager, List<PacManAgentManager>> groups,
        float hueOffset,
        float saturation,
        float value)
    {
        if (groups == null || groups.Count == 0)
            return;

        int index = 0;

        foreach (var kvp in groups)
        {
            PacManAgentManager leader = kvp.Key;
            List<PacManAgentManager> members = kvp.Value;

            if (leader == null || leader.transform == null)
                continue;

            // Generate a distinct color per group
            float hue = Mathf.Repeat(hueOffset + (index * 0.17f), 1f);
            Color groupColor = Color.HSVToRGB(hue, saturation, value);
            Color leaderColor = Color.Lerp(groupColor, Color.white, 0.25f);
            Color memberColor = Color.Lerp(groupColor, Color.black, 0.15f);

            Vector3 leaderPos = leader.transform.position;

            // Leader visualization
            Gizmos.color = leaderColor;
            Gizmos.DrawWireSphere(leaderPos, 0.6f);

            // Optional: draw a small vertical line above the leader
            Gizmos.DrawLine(leaderPos, leaderPos + Vector3.up * 1.5f);

            if (members != null)
            {
                foreach (var member in members)
                {
                    if (member == null || member.transform == null)
                        continue;

                    Vector3 memberPos = member.transform.position;

                    // Connection line leader -> member
                    Gizmos.color = groupColor;
                    Gizmos.DrawLine(leaderPos, memberPos);

                    // Member visualization
                    Gizmos.color = memberColor;
                    Gizmos.DrawSphere(memberPos, .6f);
                }
            }

            index++;
        }
    }
}
