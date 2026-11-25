using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class MonsterSpawner : MonoBehaviour
{
    [System.Serializable]
    public class MonsterSpawnRule
    {
        public int tileCount;
        public GameObject monsterPrefab;
    }

    //타일맵 설정
    [SerializeField] private Tilemap monsterTilemap;
    [SerializeField] private bool hideTilemap = true;

    //소환 규칙 목록
    [SerializeField] private List<MonsterSpawnRule> spawnRules = new List<MonsterSpawnRule>();

    //소환 타이밍
    [SerializeField] private bool spawnOnStart = false;

    //살아있는 몬스터 확인 간격 (초)
    [SerializeField] private float aliveCheckInterval = 0.3f;

    //실제로 1마리 이상 소환되었을 때 호출
    public UnityEvent onMonstersSpawned;

    //살아있는 몬스터가 1마리 이상 → 0마리가 되었을 때 호출 (예: DoorController.OpenDoor 연결)
    public UnityEvent onAllMonstersDead;

    //플레이어 참조 (씬이 시작될 때마다 씬에 있는 플레이어를 자동으로 찾음)
    private Transform player;

    private bool hasSpawned = false;

    //타일 클러스터(소환 위치) 캐시와 클러스터별로 소환된 몬스터
    private List<List<Vector3Int>> clusters;
    private GameObject[] spawnedPerCluster;

    private Coroutine aliveCheckRoutine;

    //현재 살아있는 몬스터 수
    public int AliveCount
    {
        get
        {
            if (spawnedPerCluster == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var monster in spawnedPerCluster)
            {
                if (monster != null)
                {
                    count++;
                }
            }
            return count;
        }
    }

    private void Start()
    {
        FindPlayer();

        if (hideTilemap && monsterTilemap != null)
        {
            TilemapRenderer tr = monsterTilemap.GetComponent<TilemapRenderer>();
            if (tr != null)
            {
                tr.enabled = false;
            }
        }

        if (spawnOnStart)
        {
            SpawnMonsters();
        }
    }

    //1회만 소환 (기존 방식)
    public void SpawnMonsters()
    {
        if (hasSpawned)
        {
            return;
        }

        hasSpawned = true;
        SpawnInEmptySlots();
    }

    //죽어서 비어있는 위치에만 다시 소환 (횟수 제한 없음, MonsterSpawnTimer에서 사용)
    public void RespawnDeadMonsters()
    {
        hasSpawned = true;
        SpawnInEmptySlots();
    }

    private void SpawnInEmptySlots()
    {
        if (monsterTilemap == null)
        {
            return;
        }

        //Start 이전에 소환이 호출되었거나 플레이어가 바뀐 경우 대비
        if (player == null)
        {
            FindPlayer();
        }

        //타일맵은 게임 중 바뀌지 않으므로 클러스터는 한 번만 계산
        if (clusters == null)
        {
            clusters = FindTileClusters();
            spawnedPerCluster = new GameObject[clusters.Count];
        }

        int spawnedCount = 0;

        for (int i = 0; i < clusters.Count; i++)
        {
            //아직 살아있는 몬스터가 있는 위치는 건너뜀
            if (spawnedPerCluster[i] != null)
            {
                continue;
            }

            GameObject prefab = GetPrefabForTileCount(clusters[i].Count);
            if (prefab == null)
            {
                continue;
            }

            Vector3 spawnPos = CalculateClusterCenter(clusters[i]);
            GameObject monster = Instantiate(prefab, spawnPos, Quaternion.identity);

            //소환된 몬스터에 씬의 플레이어 등록 (몬스터의 Start보다 먼저 실행됨)
            Monster monsterComponent = monster.GetComponent<Monster>();
            if (monsterComponent != null && player != null)
            {
                monsterComponent.player = player;
            }

            spawnedPerCluster[i] = monster;
            spawnedCount++;
        }

        if (spawnedCount > 0)
        {
            onMonstersSpawned?.Invoke();

            if (aliveCheckRoutine == null)
            {
                aliveCheckRoutine = StartCoroutine(AliveCheckRoutine());
            }
        }
    }

    //살아있는 몬스터가 0마리가 될 때까지 감시
    private IEnumerator AliveCheckRoutine()
    {
        while (AliveCount > 0)
        {
            yield return new WaitForSeconds(aliveCheckInterval);
        }

        aliveCheckRoutine = null;
        onAllMonstersDead?.Invoke();
    }

    //씬에 있는 'Player' 태그 오브젝트를 찾아 플레이어로 등록
    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("MonsterSpawner: 'Player' 태그를 가진 오브젝트를 찾지 못했습니다.");
        }
    }

    private List<List<Vector3Int>> FindTileClusters()
    {
        List<List<Vector3Int>> clusters = new List<List<Vector3Int>>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

        BoundsInt bounds = monsterTilemap.cellBounds;
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left };

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);

                if(!monsterTilemap.HasTile(pos) || visited.Contains(pos))
                {
                    continue;
                }

                List<Vector3Int> currentCluster = new List<Vector3Int>();
                Queue<Vector3Int> queue = new Queue<Vector3Int>();

                queue.Enqueue(pos);
                visited.Add(pos);

                while(queue.Count > 0)
                {
                    Vector3Int current = queue.Dequeue();
                    currentCluster.Add(current);

                    foreach(var dir in directions)
                    {
                        Vector3Int neighbor = current + dir;
                        if(monsterTilemap.HasTile(neighbor) && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                clusters.Add(currentCluster);
            }
        }
        return clusters;
    }

    private Vector3 CalculateClusterCenter(List<Vector3Int> cluster)
    {
        Vector3 sumPos = Vector3.zero;
        foreach(var cellPos in cluster)
        {
            sumPos += monsterTilemap.GetCellCenterWorld(cellPos);
        }
        return sumPos / cluster.Count;
    }

    private GameObject GetPrefabForTileCount(int count)
    {
        foreach (var rule in spawnRules)
        {
            if(rule.tileCount == count)
            {
                return rule.monsterPrefab;
            }
        }
        return null;
    }
}
