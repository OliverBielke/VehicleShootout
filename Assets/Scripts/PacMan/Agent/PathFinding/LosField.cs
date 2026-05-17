using System;
using System.Collections.Generic;
using PacMan.Local;
using Scripts.Map;
using UnityEngine;

namespace PacMan.Agent.PathFinding
{
    public struct LosAgentData
    {
        public List<Vector3> EnemyPositions;
        public float losMultiplier;
    }
    public class LosField :  MonoBehaviour
    {
        [SerializeField] MapManager mapManager;
        public float GlobalDangerMultiplier = 1f;
        public static LosField instance;

        private void Awake()
        {
            if (instance != null)
            {
                Debug.LogWarning("Multiple instances of LOS field found, destroying...");
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void Start()
        {
            obstacleMap = ObstacleMap.Initialize(mapManager, new List<GameObject>(), cellScale: new Vector3(CellScale, 1f, CellScale));
            losMap = preCalculateLosField();
        }
        
        public ObstacleMap obstacleMap;
        public float CellScale = 1f;
        private Dictionary<Vector2Int, HashSet<Vector2Int>> losMap = new  Dictionary<Vector2Int, HashSet<Vector2Int>>();


        public float GetDanger(Vector3 worldPos, LosAgentData agentData)
        {
            float danger = 0f;
            Vector3Int originCell = obstacleMap.WorldToCell(worldPos);
            Vector2Int originKey = new Vector2Int(originCell.x, originCell.z);
            if (!losMap.ContainsKey(originKey))
            {
                Debug.LogWarning("No LOS map entry found for position in GetDanger, returning danger = 1!");
                return 0f;
            }

            foreach (Vector3 enemyPos in agentData.EnemyPositions)
            {
                Vector3Int enemyCell = obstacleMap.WorldToCell(enemyPos);
                Vector2Int enemyKey = new Vector2Int(enemyCell.x, enemyCell.z);
                if (losMap[originKey].Contains(enemyKey))
                {
                    danger += (1 - Mathf.Clamp01(Vector3.Distance(worldPos, enemyPos)/20f))*GlobalDangerMultiplier*agentData.losMultiplier;
                }
            }
            return danger;
        }
        
        
        /// <summary>
        /// Creates a LOS field for one enemy
        /// </summary>
        /// <param name="enemy">Enemy to check LOS from.</param>
        /// <param name="unitsPerSquare">Number of units to check LOS from for every square in the game</param>
        /// <returns></returns>
        private Dictionary<Vector2Int, HashSet<Vector2Int>> preCalculateLosField()
        {
            float startTime = Time.time;
            HashSet<Vector3Int> checkedTiles = new HashSet<Vector3Int>();
            Dictionary<Vector2Int,  HashSet<Vector2Int>> field = new Dictionary<Vector2Int,  HashSet<Vector2Int>>();
            if (obstacleMap == null)
            {
                Debug.LogWarning("ObstacleMap is null when computing LOS for enemy, returning...");
                return field;
            }
            Debug.Log($"Creating LOS field from x = {obstacleMap.cellBounds.xMin} to {obstacleMap.cellBounds.xMax} and z = {obstacleMap.cellBounds.zMin} to {obstacleMap.cellBounds.zMax} with cell scale {CellScale}...");
            foreach (var cellPos in obstacleMap.cellBounds.allPositionsWithin)
            {
                var cellKey = new Vector2Int(cellPos.x, cellPos.z);
                //if (!obstacleMap.traversabilityPerCell.TryGetValue(cellKey, out var trav) ||
                //    trav != ObstacleMap.Traversability.Free)
                //    continue;

                populateLosField(field, checkedTiles, cellPos);
                checkedTiles.Add(cellPos);
            }
            Debug.Log($"Finished precomputing LOS field with {field.Count} entries in {Time.time - startTime} seconds!");
            return field;
        }
        
        private void populateLosField(Dictionary<Vector2Int, HashSet<Vector2Int>> field, HashSet<Vector3Int> toCheck, Vector3Int originCell)
        {
            Vector2Int originKey = new Vector2Int(originCell.x, originCell.z);
            Vector3 originPos = obstacleMap.CellToWorld(originCell) + obstacleMap.trueScale / 2f;
            HashSet<Vector2Int> visibleCells;
            if (field.TryGetValue(originKey, out HashSet<Vector2Int> losField))
            {
                visibleCells = losField;
            }
            else
            {
                visibleCells = new HashSet<Vector2Int>();
            }

            foreach (Vector3Int targetCell in toCheck)
            {
                Vector2Int targetKey = new Vector2Int(targetCell.x, targetCell.z);
                Vector3 targetPos = obstacleMap.CellToWorld(targetCell) + obstacleMap.trueScale / 2f;

                if (Physics.Raycast(originPos, (targetPos - originPos).normalized,
                        Vector3.Distance(targetPos, originPos), LayerMask.GetMask("Obstacle")))
                {
                    continue;
                }
                visibleCells.Add(targetKey);
                if(field.TryGetValue(targetKey, out HashSet<Vector2Int> targetLosField))
                {
                    targetLosField.Add(originKey);
                }
                else
                {
                    Debug.LogWarning("Reciprical LOS field not found  in precomputing LOS field!");
                    //field[targetKey] = new HashSet<Vector2Int>() {originKey};
                }
            }
            field[originKey] = visibleCells;
        }

        public void DrawLosField(PacManAIDebugBT agent)
        {
            Vector3Int agentCell = obstacleMap.WorldToCell(agent.transform.position);
            Vector2Int agentKey = new Vector2Int(agentCell.x, agentCell.z);
            if (!losMap.ContainsKey(agentKey))
            {
                Debug.LogWarning("No los map entry found for agents position!");
            }
                
            HashSet<Vector2Int> visibleCells = losMap[agentKey];
            foreach (Vector2Int visibleCell in visibleCells)
            {
                Vector3Int cellLocation = new Vector3Int(visibleCell.x, 0, visibleCell.y);
                Vector3 pos = obstacleMap.CellToWorld(cellLocation)+ obstacleMap.trueScale / 2f; 
                Gizmos.color = Color.blue - new Color(0, 0, 0, 0.5f);
                Gizmos.DrawCube(pos, obstacleMap.trueScale);
            }

        }
    }
    
}
