using ArenaBreak.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaBreak.Player
{
    public class WeaponSystem : MonoBehaviour
    {
        private const string ActionMapName = "Player";
        private const string AttackActionName = "Attack";
        private const string ReloadBinding = "<Keyboard>/r";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Aim")]
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Fire")]
        [SerializeField] private float _fireInterval = 0.15f;
        [SerializeField] private float _range = 50f;
        [SerializeField] private int _damage = 10;

        [Header("Ammo")]
        [SerializeField] private int _magazineSize = 12;
        [SerializeField] private float _reloadDuration = 1.5f;

        [Header("Hit Marker (Debug)")]
        [SerializeField] private Color _hitMarkerColor = Color.yellow;
        [SerializeField] private float _hitMarkerSize = 0.2f;
        [SerializeField] private float _hitMarkerLifetime = 0.5f;

        private InputAction _attackAction;
        // 에셋에 R 액션이 없어서 코드에서 직접 만든다. 소유하므로 직접 Dispose 해야 한다
        private InputAction _reloadAction;
        private MaterialPropertyBlock _markerBlock;

        private int _ammo;
        private float _nextFireTime;
        private bool _isReloading;
        private float _reloadEndTime;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponentInChildren<Camera>();
            }

            InputActionMap map = _inputActions.FindActionMap(ActionMapName, true);
            _attackAction = map.FindAction(AttackActionName, true);

            _reloadAction = new InputAction("Reload", InputActionType.Button, ReloadBinding);

            _markerBlock = new MaterialPropertyBlock();
            _markerBlock.SetColor(BaseColorId, _hitMarkerColor);

            _ammo = _magazineSize;
        }

        private void OnEnable()
        {
            // 맵 전체가 아니라 액션만 켜고 끈다. 맵은 PlayerController와 공유하므로
            // 여기서 맵을 끄면 이동·시점 입력까지 같이 죽는다
            _attackAction.Enable();
            _reloadAction.Enable();
        }

        private void OnDisable()
        {
            _attackAction.Disable();
            _reloadAction.Disable();
            _isReloading = false;
        }

        private void OnDestroy()
        {
            _reloadAction?.Dispose();
        }

        private void Update()
        {
            if (_isReloading)
            {
                if (Time.time >= _reloadEndTime)
                {
                    FinishReload();
                }
                return;
            }

            if (_reloadAction.WasPressedThisFrame())
            {
                StartReload();
                return;
            }

            if (_attackAction.IsPressed() && Time.time >= _nextFireTime)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (_ammo <= 0)
            {
                // 빈 탄창에서 누르고 있으면 로그가 프레임마다 쌓이므로 간격을 두고 한 번만 알린다
                _nextFireTime = Time.time + _fireInterval;
                Debug.Log("탄약 없음 - R키로 재장전");
                return;
            }

            _ammo--;
            _nextFireTime = Time.time + _fireInterval;

            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, _range, _hitMask, QueryTriggerInteraction.Ignore))
            {
                SpawnHitMarker(hit.point);

                // 맞은 콜라이더가 자식일 수 있어 부모까지 찾는다. 명중 순간에만 호출되므로 캐싱 대상이 아니다
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                target?.TakeDamage(_damage);
            }

            // HUD 이벤트로 옮기기 전까지 임시 출력
            Debug.LogFormat("발사 - 남은 탄약 {0}/{1}", _ammo, _magazineSize);
        }

        private void StartReload()
        {
            if (_ammo >= _magazineSize)
            {
                return;
            }

            _isReloading = true;
            _reloadEndTime = Time.time + _reloadDuration;
            Debug.LogFormat("재장전 시작 - 남은 탄약 {0}/{1}", _ammo, _magazineSize);
        }

        private void FinishReload()
        {
            _isReloading = false;
            _ammo = _magazineSize;
            Debug.LogFormat("재장전 완료 - 남은 탄약 {0}/{1}", _ammo, _magazineSize);
        }

        // 풀링 대상: 연사 시 생성/파괴가 반복된다. 디버그 전용이라 지금은 Destroy로 둔다
        private void SpawnHitMarker(Vector3 position)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * _hitMarkerSize;

            // 콜라이더가 남아 있으면 다음 발사 Ray가 마커에 맞는다
            Destroy(marker.GetComponent<Collider>());

            marker.GetComponent<Renderer>().SetPropertyBlock(_markerBlock);
            Destroy(marker, _hitMarkerLifetime);
        }
    }
}
