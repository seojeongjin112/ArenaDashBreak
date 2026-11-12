using System.Collections;
using ArenaBreak.Core;
using UnityEngine;

namespace ArenaBreak.Enemy
{
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _renderer;
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private float _flashDuration = 0.05f;

        private Health _health;
        // 머티리얼 인스턴스를 만들면 풀링된 적마다 머티리얼이 새므로 프로퍼티 블록으로 색만 덮어쓴다
        private MaterialPropertyBlock _block;
        private WaitForSeconds _wait;
        private Coroutine _routine;
        private int _lastHealth;

        private void Awake()
        {
            _health = GetComponent<Health>();

            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<Renderer>();
            }

            if (_renderer == null)
            {
                Debug.LogError("HitFlash: Renderer를 찾을 수 없습니다", this);
                enabled = false;
                return;
            }

            _block = new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, _flashColor);
            _wait = new WaitForSeconds(_flashDuration);
        }

        private void OnEnable()
        {
            ClearFlash();

            // 풀에서 꺼낸 적은 가득 찬 체력으로 시작한다. MaxHealth는 직렬화 값이라 Awake 순서와 무관하다
            _lastHealth = _health.MaxHealth;
            _health.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            _health.OnHealthChanged -= HandleHealthChanged;

            // 플래시 도중 풀로 돌아가면 코루틴이 끊겨 흰색이 남으므로 직접 되돌린다
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            ClearFlash();
        }

        // ResetHealth도 이 이벤트를 발행하지만 체력이 늘어나는 쪽이라 이전 값과 비교해 피격만 거른다
        private void HandleHealthChanged(int current, int max)
        {
            bool damaged = current < _lastHealth;
            _lastHealth = current;

            if (damaged)
            {
                Flash();
            }
        }

        private void Flash()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _renderer.SetPropertyBlock(_block);
            yield return _wait;
            ClearFlash();
            _routine = null;
        }

        private void ClearFlash()
        {
            if (_renderer != null)
            {
                _renderer.SetPropertyBlock(null);
            }
        }
    }
}
