using ArenaBreak.Core;
using UnityEngine;

namespace ArenaBreak.Player
{
    public class CameraShake : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private WeaponSystem _weaponSystem;

        [Header("Shake")]
        [SerializeField] private Transform _camera;
        [SerializeField] private float _duration = 0.1f;
        [SerializeField] private float _playerHitIntensity = 0.15f;
        // 연사 중에는 매 발마다 흔들리므로 플레이어 피격보다 훨씬 약하게 둔다
        [SerializeField] private float _enemyHitIntensity = 0.03f;

        private Vector3 _basePosition;
        private float _remaining;
        private float _currentIntensity;
        private int _lastPlayerHealth;

        private void Awake()
        {
            if (_playerHealth == null || _weaponSystem == null || _camera == null)
            {
                Debug.LogError("CameraShake: Player Health, Weapon System, Camera 참조가 비어 있습니다", this);
                enabled = false;
                return;
            }

            _basePosition = _camera.localPosition;
        }

        private void OnEnable()
        {
            // MaxHealth는 직렬화 값이라 Health.Awake 순서와 무관하게 읽을 수 있다
            _lastPlayerHealth = _playerHealth.MaxHealth;
            _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
            _weaponSystem.OnTargetHit += HandleTargetHit;
        }

        // Awake에서 참조 검사에 실패해 enabled를 끄면 OnDisable이 바로 호출되므로 null을 먼저 거른다
        private void OnDisable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            }

            if (_weaponSystem != null)
            {
                _weaponSystem.OnTargetHit -= HandleTargetHit;
            }

            // 흔들리는 도중에 꺼지면 카메라가 어긋난 채 남으므로 원위치로 돌린다
            _remaining = 0f;
            if (_camera != null)
            {
                _camera.localPosition = _basePosition;
            }
        }

        // PlayerController가 카메라 피벗의 회전을 매 프레임 덮어쓰므로 회전이 아니라 위치만 흔든다.
        // 이동이 끝난 뒤에 적용하려고 LateUpdate를 쓴다
        private void LateUpdate()
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining -= Time.deltaTime;

            if (_remaining <= 0f)
            {
                _camera.localPosition = _basePosition;
                return;
            }

            float falloff = _remaining / _duration;
            _camera.localPosition = _basePosition + Random.insideUnitSphere * (_currentIntensity * falloff);
        }

        private void HandlePlayerHealthChanged(int current, int max)
        {
            bool damaged = current < _lastPlayerHealth;
            _lastPlayerHealth = current;

            if (damaged)
            {
                Shake(_playerHitIntensity);
            }
        }

        private void HandleTargetHit()
        {
            Shake(_enemyHitIntensity);
        }

        private void Shake(float intensity)
        {
            // 강한 흔들림이 남아 있을 때 약한 흔들림이 덮어쓰지 못하게 한다
            float remainingIntensity = _currentIntensity * (_remaining / _duration);
            if (_remaining > 0f && intensity < remainingIntensity)
            {
                return;
            }

            _currentIntensity = intensity;
            _remaining = _duration;
        }
    }
}
