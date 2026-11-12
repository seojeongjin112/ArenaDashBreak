using System;
using System.Collections;
using System.Collections.Generic;
using ArenaBreak.Enemy;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace ArenaBreak.Core
{
    public class WaveSpawner : MonoBehaviour
    {
        private const string PoolRootName = "EnemyPool";

        [Header("Waves")]
        [SerializeField] private List<WaveData> _waves = new List<WaveData>();
        [SerializeField] private float _nextWaveDelay = 3f;

        [Header("Scene References")]
        [SerializeField] private Transform _player;
        // 이 오브젝트의 자식 Transform들을 Awake에서 모아 스폰 지점으로 쓴다
        [SerializeField] private Transform _spawnPoints;

        [Header("Spawn")]
        [SerializeField] private float _navMeshSampleDistance = 2f;

        [Header("Pool")]
        [SerializeField] private int _poolMaxSize = 50;

        private readonly Dictionary<Health, Action> _deathHandlers = new Dictionary<Health, Action>();
        // 같은 프리팹을 쓰는 웨이브끼리 풀을 공유하도록 프리팹별로 풀을 둔다
        private readonly Dictionary<EnemyAI, ObjectPool<EnemyAI>> _pools = new Dictionary<EnemyAI, ObjectPool<EnemyAI>>();
        private readonly Dictionary<EnemyAI, EnemyAI> _prefabByInstance = new Dictionary<EnemyAI, EnemyAI>();
        private readonly HashSet<EnemyAI> _activeEnemies = new HashSet<EnemyAI>();

        private Transform[] _spawnPointList;
        private Transform _poolRoot;
        private int _aliveCount;
        private int _currentWave;
        private int _killCount;

        // (현재 웨이브 번호, 전체 웨이브 수). 번호는 1부터 시작한다
        public event Action<int, int> OnWaveStarted;
        public event Action<int> OnWaveCleared;
        public event Action OnAllWavesCleared;
        // 누적 처치 수. HUD가 초기값은 KillCount 프로퍼티로 읽는다
        public event Action<int> OnKillCountChanged;

        public int CurrentWave => _currentWave;
        public int AliveEnemyCount => _aliveCount;
        public int KillCount => _killCount;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            _spawnPointList = new Transform[_spawnPoints.childCount];
            for (int i = 0; i < _spawnPointList.Length; i++)
            {
                _spawnPointList[i] = _spawnPoints.GetChild(i);
            }

            CreatePoolRoot();
        }

        private void Start()
        {
            StartCoroutine(RunWaves());
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private bool ValidateReferences()
        {
            if (_player == null || _spawnPoints == null || _spawnPoints.childCount == 0)
            {
                Debug.LogError("WaveSpawner: Player 또는 Spawn Points(자식 포함)가 비어 있습니다", this);
                return false;
            }

            if (_waves.Count == 0)
            {
                Debug.LogError("WaveSpawner: Waves 리스트가 비어 있습니다", this);
                return false;
            }

            foreach (WaveData wave in _waves)
            {
                if (wave == null || wave.EnemyPrefab == null)
                {
                    Debug.LogError("WaveSpawner: WaveData 또는 그 Enemy Prefab이 비어 있습니다", this);
                    return false;
                }
            }

            return true;
        }

        // 비활성 부모 아래에 두면 적이 스폰 위치가 정해지기 전에 Awake/OnEnable을 실행하지 않는다
        private void CreatePoolRoot()
        {
            var rootObject = new GameObject(PoolRootName);
            rootObject.SetActive(false);
            _poolRoot = rootObject.transform;
            _poolRoot.SetParent(transform);
        }

        private IEnumerator RunWaves()
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                WaveData wave = _waves[i];
                _currentWave = i + 1;

                Debug.LogFormat("웨이브 {0}/{1} 시작 - 적 {2}마리", _currentWave, _waves.Count, wave.SpawnCount);
                OnWaveStarted?.Invoke(_currentWave, _waves.Count);

                var spawnWait = new WaitForSeconds(wave.SpawnInterval);
                for (int n = 0; n < wave.SpawnCount; n++)
                {
                    SpawnEnemy(wave.EnemyPrefab);
                    yield return spawnWait;
                }

                // 스폰이 끝난 뒤에 기다려야 앞서 소환한 적이 다 죽어도 웨이브가 일찍 끝나지 않는다
                yield return new WaitUntil(HasNoAliveEnemy);

                Debug.LogFormat("웨이브 {0}/{1} 클리어", _currentWave, _waves.Count);
                OnWaveCleared?.Invoke(_currentWave);

                if (i < _waves.Count - 1)
                {
                    yield return new WaitForSeconds(_nextWaveDelay);
                }
            }

            Debug.Log("전체 웨이브 클리어");
            OnAllWavesCleared?.Invoke();
        }

        private bool HasNoAliveEnemy()
        {
            return _aliveCount <= 0;
        }

        private void SpawnEnemy(EnemyAI prefab)
        {
            Transform point = _spawnPointList[Random.Range(0, _spawnPointList.Length)];

            // NavMesh 밖에 소환되면 NavMeshAgent가 동작하지 않으므로 가까운 NavMesh 위로 옮긴다
            if (!NavMesh.SamplePosition(point.position, out NavMeshHit hit, _navMeshSampleDistance, NavMesh.AllAreas))
            {
                Debug.LogWarning("WaveSpawner: 스폰 지점 근처에 NavMesh가 없어 건너뜁니다 - " + point.name, point);
                return;
            }

            EnemyAI enemy = GetPool(prefab).Get();

            // 위치를 먼저 정한 뒤 풀 루트에서 꺼내야, 활성화되는 순간 NavMeshAgent가 올바른 자리에 놓인다
            enemy.transform.SetPositionAndRotation(hit.position, point.rotation);
            enemy.transform.SetParent(null);
            enemy.SetTarget(_player);

            _activeEnemies.Add(enemy);
            enemy.OnReadyToDespawn += HandleEnemyReadyToDespawn;
            TrackDeath(enemy.GetComponent<Health>());
        }

        private ObjectPool<EnemyAI> GetPool(EnemyAI prefab)
        {
            if (_pools.TryGetValue(prefab, out ObjectPool<EnemyAI> pool))
            {
                return pool;
            }

            pool = new ObjectPool<EnemyAI>(
                createFunc: () => CreateEnemy(prefab),
                actionOnRelease: ReturnToPoolRoot,
                actionOnDestroy: DestroyEnemy,
                collectionCheck: true,
                maxSize: _poolMaxSize);

            _pools.Add(prefab, pool);
            return pool;
        }

        private EnemyAI CreateEnemy(EnemyAI prefab)
        {
            EnemyAI enemy = Instantiate(prefab, _poolRoot);
            _prefabByInstance[enemy] = prefab;
            return enemy;
        }

        // 부모를 비활성 루트로 옮기면 적이 하이어라키에서 비활성화된다
        private void ReturnToPoolRoot(EnemyAI enemy)
        {
            enemy.transform.SetParent(_poolRoot);
        }

        private void DestroyEnemy(EnemyAI enemy)
        {
            _prefabByInstance.Remove(enemy);

            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }

        private void HandleEnemyReadyToDespawn(EnemyAI enemy)
        {
            enemy.OnReadyToDespawn -= HandleEnemyReadyToDespawn;
            _activeEnemies.Remove(enemy);
            _pools[_prefabByInstance[enemy]].Release(enemy);
        }

        private void TrackDeath(Health health)
        {
            _aliveCount++;

            // OnDied가 인자 없는 이벤트라 어떤 적이 죽었는지 알 수 없어서 적마다 핸들러를 만든다
            Action handler = () => HandleEnemyDied(health);
            _deathHandlers[health] = handler;
            health.OnDied += handler;
        }

        private void HandleEnemyDied(Health health)
        {
            if (_deathHandlers.TryGetValue(health, out Action handler))
            {
                health.OnDied -= handler;
                _deathHandlers.Remove(health);
            }

            _aliveCount--;
            _killCount++;
            OnKillCountChanged?.Invoke(_killCount);
        }

        private void UnsubscribeAll()
        {
            foreach (KeyValuePair<Health, Action> pair in _deathHandlers)
            {
                // 풀링되는 적은 파괴되지 않지만, 외부에서 파괴된 대상은 건너뛴다
                if (pair.Key != null)
                {
                    pair.Key.OnDied -= pair.Value;
                }
            }

            _deathHandlers.Clear();

            foreach (EnemyAI enemy in _activeEnemies)
            {
                if (enemy != null)
                {
                    enemy.OnReadyToDespawn -= HandleEnemyReadyToDespawn;
                }
            }

            _activeEnemies.Clear();
        }
    }
}
