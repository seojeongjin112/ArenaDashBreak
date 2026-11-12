using System;
using ArenaBreak.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace ArenaBreak.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        private enum EnemyState
        {
            Idle,
            Chase,
            Attack,
            Dead
        }

        [Header("Target")]
        [SerializeField] private Transform _player;

        [Header("Range")]
        [SerializeField] private float _chaseRange = 60f;
        [SerializeField] private float _attackRange = 2f;

        [Header("Attack")]
        [SerializeField] private float _attackInterval = 2f;
        [SerializeField] private int _attackDamage = 10;

        [Header("Movement")]
        // 웨이브에서 적이 많아지면 매 프레임 경로 재계산이 비싸서 간격을 둔다
        [SerializeField] private float _repathInterval = 0.2f;

        [Header("Death")]
        // 이름을 바꿔도 프리팹에 저장된 값이 유지되도록 이전 직렬화 이름을 남긴다
        [FormerlySerializedAs("_destroyDelay")]
        [SerializeField] private float _despawnDelay = 1f;

        private NavMeshAgent _agent;
        private Health _health;
        private IDamageable _playerDamageable;

        private EnemyState _state = EnemyState.Idle;
        private float _nextAttackTime;
        private float _nextRepathTime;
        private float _despawnTime;

        // 사망 연출이 끝나 회수할 차례가 되면 알린다. 구독자가 풀로 돌려보내고, 없으면 스스로 파괴한다
        public event Action<EnemyAI> OnReadyToDespawn;

        // 프리팹은 씬 오브젝트를 참조할 수 없으므로 스폰한 쪽이 플레이어를 넣어준다
        public void SetTarget(Transform player)
        {
            _player = player;
            CachePlayerDamageable();
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            CachePlayerDamageable();
        }

        private void OnEnable()
        {
            // 풀에서 다시 꺼내질 때마다 호출되므로 이전 생의 상태를 여기서 초기화한다
            _state = EnemyState.Idle;
            _nextAttackTime = 0f;
            _nextRepathTime = 0f;
            _despawnTime = 0f;
            _health.ResetHealth();

            _health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            _health.OnDied -= HandleDied;
        }

        private void Update()
        {
            if (_state == EnemyState.Dead)
            {
                if (Time.time >= _despawnTime)
                {
                    Despawn();
                }
                return;
            }

            if (_player == null)
            {
                if (_state != EnemyState.Idle)
                {
                    ChangeState(EnemyState.Idle);
                }
                return;
            }

            float sqrDistance = GetPlanarSqrDistance();

            switch (_state)
            {
                case EnemyState.Idle:
                    if (sqrDistance <= _chaseRange * _chaseRange)
                    {
                        ChangeState(EnemyState.Chase);
                    }
                    break;

                case EnemyState.Chase:
                    UpdateChase(sqrDistance);
                    break;

                case EnemyState.Attack:
                    UpdateAttack(sqrDistance);
                    break;
            }
        }

        private void UpdateChase(float sqrDistance)
        {
            if (sqrDistance > _chaseRange * _chaseRange)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            if (sqrDistance <= _attackRange * _attackRange)
            {
                ChangeState(EnemyState.Attack);
                return;
            }

            if (Time.time >= _nextRepathTime && _agent.isOnNavMesh)
            {
                _agent.SetDestination(_player.position);
                _nextRepathTime = Time.time + _repathInterval;
            }
        }

        private void UpdateAttack(float sqrDistance)
        {
            if (sqrDistance > _attackRange * _attackRange)
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            FacePlayer();

            if (Time.time >= _nextAttackTime)
            {
                Attack();
            }
        }

        private void Attack()
        {
            _nextAttackTime = Time.time + _attackInterval;

            // 쿨다운을 상태 진입이 아니라 여기서 갱신해야 범위를 들락거려도 공격 간격을 우회하지 못한다
            _playerDamageable?.TakeDamage(_attackDamage);

            Debug.LogFormat("{0} 공격 - 데미지 {1} (플레이어 Health 연결: {2})",
                name, _attackDamage, _playerDamageable != null);
        }

        private void FacePlayer()
        {
            Vector3 direction = _player.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void ChangeState(EnemyState newState)
        {
            _state = newState;

            switch (newState)
            {
                case EnemyState.Idle:
                case EnemyState.Attack:
                    SetAgentStopped(true);
                    break;

                case EnemyState.Chase:
                    _nextRepathTime = 0f;
                    SetAgentStopped(false);
                    break;

                case EnemyState.Dead:
                    SetAgentStopped(true);
                    _despawnTime = Time.time + _despawnDelay;
                    break;
            }
        }

        private void HandleDied()
        {
            ChangeState(EnemyState.Dead);
        }

        private void Despawn()
        {
            // 같은 프레임에 다시 들어오지 않도록 먼저 막는다
            _despawnTime = Mathf.Infinity;

            if (OnReadyToDespawn != null)
            {
                OnReadyToDespawn.Invoke(this);
            }
            else
            {
                // 스포너 없이 씬에 직접 놓인 적은 회수할 곳이 없으니 예전처럼 파괴한다
                Destroy(gameObject);
            }
        }

        // NavMesh 밖에 스폰되면 isStopped 접근이 에러를 내므로 먼저 확인한다
        private void SetAgentStopped(bool stopped)
        {
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = stopped;
            }
        }

        // 플레이어 피벗이 허리 높이라 3D 거리를 쓰면 공격 범위가 실제보다 짧아진다
        private float GetPlanarSqrDistance()
        {
            Vector3 offset = _player.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }

        private void CachePlayerDamageable()
        {
            _playerDamageable = _player != null ? _player.GetComponentInParent<IDamageable>() : null;
        }
    }
}
