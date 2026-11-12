using ArenaBreak.Core;
using ArenaBreak.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaBreak.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private WeaponSystem _weaponSystem;
        [SerializeField] private WaveSpawner _waveSpawner;
        [SerializeField] private GameManager _gameManager;

        [Header("Health Bar")]
        // 앵커 Min이 (0,0), Max가 (1,1)이고 오프셋이 0인 상태에서 Max.x를 줄여 폭을 바꾼다
        [SerializeField] private RectTransform _healthFill;

        [Header("Texts")]
        [SerializeField] private Text _ammoText;
        [SerializeField] private Text _waveText;
        [SerializeField] private Text _killText;

        [Header("Result")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultText;

        [Header("Formats")]
        [SerializeField] private string _ammoFormat = "{0} / {1}";
        [SerializeField] private string _waveFormat = "WAVE {0} / {1}";
        [SerializeField] private string _killFormat = "KILLS {0}";
        [SerializeField] private string _gameOverMessage = "GAME OVER";
        [SerializeField] private string _clearedMessage = "CLEARED";

        private void Awake()
        {
            if (_playerHealth == null || _weaponSystem == null || _waveSpawner == null || _gameManager == null
                || _healthFill == null || _ammoText == null || _waveText == null || _killText == null
                || _resultPanel == null || _resultText == null)
            {
                Debug.LogError("HUDController: 인스펙터 참조가 비어 있습니다", this);
                enabled = false;
                return;
            }

            _resultPanel.SetActive(false);
        }

        private void OnEnable()
        {
            _playerHealth.OnHealthChanged += HandleHealthChanged;
            _weaponSystem.OnAmmoChanged += HandleAmmoChanged;
            _waveSpawner.OnWaveStarted += HandleWaveStarted;
            _waveSpawner.OnKillCountChanged += HandleKillCountChanged;
            _gameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _weaponSystem.OnAmmoChanged -= HandleAmmoChanged;
            _waveSpawner.OnWaveStarted -= HandleWaveStarted;
            _waveSpawner.OnKillCountChanged -= HandleKillCountChanged;
            _gameManager.OnStateChanged -= HandleStateChanged;
        }

        // 이벤트는 값이 바뀔 때만 오므로 초기 표시는 모든 Awake가 끝난 Start에서 프로퍼티로 읽는다
        private void Start()
        {
            HandleHealthChanged(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            HandleAmmoChanged(_weaponSystem.Ammo, _weaponSystem.MagazineSize);
            HandleKillCountChanged(_waveSpawner.KillCount);
        }

        private void HandleHealthChanged(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            // fillAmount 대신 앵커 폭을 쓴다. 스프라이트 없는 Image에서는 fillAmount가 먹지 않는다
            Vector2 anchorMax = _healthFill.anchorMax;
            anchorMax.x = ratio;
            _healthFill.anchorMax = anchorMax;
        }

        private void HandleAmmoChanged(int current, int max)
        {
            _ammoText.text = string.Format(_ammoFormat, current, max);
        }

        private void HandleWaveStarted(int current, int total)
        {
            _waveText.text = string.Format(_waveFormat, current, total);
        }

        private void HandleKillCountChanged(int total)
        {
            _killText.text = string.Format(_killFormat, total);
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.GameOver:
                    ShowResult(_gameOverMessage);
                    break;

                case GameState.Cleared:
                    ShowResult(_clearedMessage);
                    break;

                default:
                    _resultPanel.SetActive(false);
                    break;
            }
        }

        private void ShowResult(string message)
        {
            _resultText.text = message;
            _resultPanel.SetActive(true);
        }
    }
}
