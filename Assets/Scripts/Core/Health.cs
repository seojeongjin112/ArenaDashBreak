using System;
using UnityEngine;

namespace ArenaBreak.Core
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int _maxHealth = 100;
        // 0이면 무적 없음. 적은 0, 플레이어만 값을 준다
        [SerializeField] private float _invulnerabilityDuration;

        // 밖에서는 프로퍼티로 읽기만 하지만, 플레이 중 인스펙터에서 값을 눈으로 검증하려고 직렬화한다
        [SerializeField] private int _currentHealth;
        private float _invulnerableUntil;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public bool IsDead => _currentHealth <= 0;

        // (현재 체력, 최대 체력). 구독 시점의 초기값은 프로퍼티로 읽어 간다
        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        // 풀에서 재사용되는 적이 이전 생의 사망 상태와 무적 시간을 물려받지 않도록 되돌린다
        public void ResetHealth()
        {
            _currentHealth = _maxHealth;
            _invulnerableUntil = 0f;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void TakeDamage(int amount)
        {
            // 사망 후 연사 중인 총알이 OnDied를 두 번 일으키지 않도록 막는다
            if (IsDead || amount <= 0 || Time.time < _invulnerableUntil)
            {
                return;
            }

            _currentHealth = Mathf.Max(_currentHealth - amount, 0);
            _invulnerableUntil = Time.time + _invulnerabilityDuration;

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (IsDead)
            {
                OnDied?.Invoke();
            }
        }
    }
}
