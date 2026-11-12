using System;
using System.Collections;
using ArenaBreak.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ArenaBreak.Core
{
    public enum GameState
    {
        Ready,
        Playing,
        GameOver,
        Cleared
    }

    public class GameManager : MonoBehaviour
    {
        private const float PausedTimeScale = 0f;
        private const float NormalTimeScale = 1f;
        private const string RestartBinding = "<Keyboard>/r";
        private const string QuitBinding = "<Keyboard>/escape";

        [Header("Flow")]
        [SerializeField] private float _readyDuration = 2f;

        [Header("Scene References")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private WaveSpawner _waveSpawner;

        // 타입별로 따로 받는다. 배열로 묶으면 인스펙터에 엉뚱한 컴포넌트가 들어갈 수 있다
        [Header("Player Control")]
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private WeaponSystem _weaponSystem;

        // 에셋에 R, Esc 액션이 없어서 코드에서 직접 만든다. 소유하므로 직접 Dispose 해야 한다
        private InputAction _restartAction;
        private InputAction _quitAction;

        private GameState _state = GameState.Ready;

        // HUD가 구독한다. 초기 상태(Ready)는 이벤트가 아니라 State 프로퍼티로 읽는다
        public event Action<GameState> OnStateChanged;

        public GameState State => _state;

        private void Awake()
        {
            if (_playerHealth == null || _waveSpawner == null || _playerController == null || _weaponSystem == null)
            {
                Debug.LogError("GameManager: Player Health, Wave Spawner, Player Controller, Weapon System 참조가 비어 있습니다", this);
                enabled = false;
                return;
            }

            _restartAction = new InputAction("Restart", InputActionType.Button, RestartBinding);
            _quitAction = new InputAction("Quit", InputActionType.Button, QuitBinding);
        }

        private void OnEnable()
        {
            _restartAction.Enable();
            _quitAction.Enable();

            _playerHealth.OnDied += HandlePlayerDied;
            _waveSpawner.OnAllWavesCleared += HandleAllWavesCleared;
        }

        private void OnDisable()
        {
            _restartAction.Disable();
            _quitAction.Disable();

            _playerHealth.OnDied -= HandlePlayerDied;
            _waveSpawner.OnAllWavesCleared -= HandleAllWavesCleared;
        }

        private void OnDestroy()
        {
            _restartAction?.Dispose();
            _quitAction?.Dispose();
        }

        private void Start()
        {
            StartCoroutine(ReadyRoutine());
        }

        private void Update()
        {
            // timeScale이 0이어도 입력은 계속 들어오므로 어느 상태에서든 동작한다
            if (_quitAction.WasPressedThisFrame())
            {
                Quit();
                return;
            }

            switch (_state)
            {
                case GameState.GameOver:
                case GameState.Cleared:
                    if (_restartAction.WasPressedThisFrame())
                    {
                        Restart();
                    }
                    break;
            }
        }

        private IEnumerator ReadyRoutine()
        {
            yield return new WaitForSeconds(_readyDuration);

            // 대기 중에 이미 끝났다면 Playing으로 되돌리지 않는다
            if (_state == GameState.Ready)
            {
                ChangeState(GameState.Playing);
            }
        }

        private void HandlePlayerDied()
        {
            EndGame(GameState.GameOver);
        }

        private void HandleAllWavesCleared()
        {
            EndGame(GameState.Cleared);
        }

        private void EndGame(GameState finalState)
        {
            // 마지막 적과 플레이어가 같은 프레임에 죽어도 먼저 정해진 결과를 유지한다
            if (_state == GameState.GameOver || _state == GameState.Cleared)
            {
                return;
            }

            ChangeState(finalState);
        }

        private void ChangeState(GameState newState)
        {
            _state = newState;

            switch (newState)
            {
                case GameState.GameOver:
                case GameState.Cleared:
                    Time.timeScale = PausedTimeScale;

                    // timeScale 0은 Update를 멈추지 않으므로 끄지 않으면 죽은 뒤에도 총이 나간다
                    _playerController.enabled = false;
                    _weaponSystem.enabled = false;

                    // PlayerController가 꺼질 때 커서를 되돌리지만, 잠금 옵션과 무관하게 확실히 보이게 한다
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }

            // HUD 이벤트로 옮기기 전까지 임시 출력
            Debug.LogFormat("게임 상태: {0}", newState);
            OnStateChanged?.Invoke(newState);
        }

        private void Restart()
        {
            // 0인 채로 로드하면 다음 씬도 멈춘 상태로 시작한다
            Time.timeScale = NormalTimeScale;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Quit()
        {
            Application.Quit();

#if UNITY_EDITOR
            // 에디터에서는 Application.Quit이 아무 일도 하지 않아서 플레이 모드를 끈다
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
