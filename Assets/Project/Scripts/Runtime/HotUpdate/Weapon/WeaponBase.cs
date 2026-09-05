using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using GamePlay;
using UnityEngine;
using YooAsset;

namespace CommandoRobot
{
    public class WeaponBase : MonoBehaviour
    {
        private const string DefaultPackageName = "DefaultPackage";

        [Header("Table")]
        [SerializeField]
        private WeaponId _weaponId = WeaponId.Rifle;

        [HideInInspector] public bool WeaponEnable = true;
        [HideInInspector] public int AmmoCount;
        [HideInInspector] public GameObject m_Owner;
        [HideInInspector] public CharacterBase m_OwnerCharacter;
        [HideInInspector] public PlayerControl Owner;
        [HideInInspector] public float FireDelayTimer;
        [HideInInspector] public float RecoilTimer;
        [HideInInspector] public WeaponModel WeaponModel;
        [HideInInspector] public bool Input_FireHold;
        [HideInInspector] public Vector3 Forward;
        [HideInInspector] public int m_PowerType;
        [HideInInspector] public int m_PowerAmmo;

        public Transform m_ParticlePoint;

        private WeaponConfig _config;
        private int _loadVersion;
        private bool _assetsReady;
        private CancellationTokenSource _loadCts;
        private AssetHandle _projectileHandle;
        private AssetHandle _modelHandle;
        private AssetHandle _effectHandle;
        private AssetHandle _iconHandle;
        private GameObject _projectilePrefab;
        private GameObject _weaponModelPrefab;
        private GameObject _effectPrefab;
        private Sprite _weaponIcon;

        public WeaponId WeaponId => _weaponId;
        public WeaponConfig Config
        {
            get => _config;
            set => _config = value;
        }

        public bool AssetsReady => _assetsReady;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public GameObject WeaponModelPrefab => _weaponModelPrefab;
        public GameObject EffectPrefab => _effectPrefab;
        public Sprite WeaponIcon => _weaponIcon;

        void Start()
        {
            if (m_ParticlePoint == null)
                m_ParticlePoint = transform;

            // 场景内嵌武器（未走 SetWeapon）自行拉表加载
            if (_loadVersion == 0)
                ApplyTableConfigAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        void OnDestroy()
        {
            CancelLoad();
            ReleaseHandles();

            // WeaponModel 会挂到角色 GunPoints，不随本物体自动销毁
            if (WeaponModel != null)
            {
                Destroy(WeaponModel.gameObject);
                WeaponModel = null;
            }
        }

        public void SetWeaponId(WeaponId weaponId)
        {
            _weaponId = weaponId;
            ApplyTableConfigAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 读表写入 Config，并异步加载 Prefab/Icon 实例后赋给本组件持有字段。
        /// </summary>
        public async UniTask ApplyTableConfigAsync(CancellationToken cancellationToken = default)
        {
            int version = ++_loadVersion;
            _assetsReady = false;

            if (GameFrameWork.DataTable == null ||
                !GameFrameWork.DataTable.TryGetTable(out IWeapon table) ||
                table == null)
            {
                Debug.LogWarning($"[WeaponBase] DataTable/IWeapon unavailable, keep current values. id={_weaponId}");
                return;
            }

            if (!table.TryGet((int)_weaponId, out Weapon_Record record))
            {
                Debug.LogError($"[WeaponBase] Weapon id not found: {_weaponId}");
                return;
            }

            _config = WeaponConfig.FromRecord(record);
            _weaponId = _config.WeaponId;
            AmmoCount = _config.InitAmmo;
            await LoadAssetsAsync(version, cancellationToken);
        }

        private async UniTask LoadAssetsAsync(int version, CancellationToken externalCt)
        {
            CancelLoad();
            _loadCts = CancellationTokenSource.CreateLinkedTokenSource(
                externalCt,
                this.GetCancellationTokenOnDestroy());
            CancellationToken ct = _loadCts.Token;

            ReleaseHandles();
            _projectilePrefab = null;
            _weaponModelPrefab = null;
            _effectPrefab = null;
            _weaponIcon = null;

            try
            {
                if (!YooAssets.TryGetPackage(DefaultPackageName, out ResourcePackage package) || package == null)
                {
                    Debug.LogWarning($"[WeaponBase] Package '{DefaultPackageName}' unavailable.");
                    return;
                }

                UniTask<GameObject> projectileTask = LoadPrefabAsync(package, _config.ProjectilePrefabLocation, h => _projectileHandle = h, ct);
                UniTask<GameObject> modelTask = LoadPrefabAsync(package, _config.WeaponModelPrefabLocation, h => _modelHandle = h, ct);
                UniTask<GameObject> effectTask = LoadPrefabAsync(package, _config.EffectPrefabLocation, h => _effectHandle = h, ct);
                UniTask<Sprite> iconTask = LoadSpriteAsync(package, _config.WeaponIconLocation, h => _iconHandle = h, ct);

                (GameObject projectile, GameObject model, GameObject effect, Sprite icon) =
                    await UniTask.WhenAll(projectileTask, modelTask, effectTask, iconTask);

                if (ct.IsCancellationRequested || version != _loadVersion)
                    return;

                _projectilePrefab = projectile;
                _weaponModelPrefab = model;
                _effectPrefab = effect;
                _weaponIcon = icon;
                _assetsReady = true;
            }
            catch (OperationCanceledException)
            {
                // 换枪 / 销毁：正常取消
            }
            catch (Exception ex)
            {
                ReleaseHandles();
                if (version == _loadVersion)
                    Debug.LogError($"[WeaponBase] Load assets failed id={_weaponId}: {ex.Message}");
            }
        }

        private static async UniTask<GameObject> LoadPrefabAsync(
            ResourcePackage package,
            string location,
            Action<AssetHandle> storeHandle,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(location))
                return null;

            AssetHandle handle = package.LoadAssetAsync<GameObject>(location);
            storeHandle(handle);
            try
            {
                await handle.ToUniTask().AttachExternalCancellation(ct);
            }
            catch
            {
                handle.Release();
                storeHandle(null);
                throw;
            }

            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                handle.Release();
                storeHandle(null);
                Debug.LogWarning($"[WeaponBase] Load prefab '{location}' failure: {error}");
                return null;
            }

            return handle.GetAssetObject<GameObject>();
        }

        private static async UniTask<Sprite> LoadSpriteAsync(
            ResourcePackage package,
            string location,
            Action<AssetHandle> storeHandle,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(location))
                return null;

            AssetHandle handle = package.LoadAssetAsync<UnityEngine.Object>(location);
            storeHandle(handle);
            try
            {
                await handle.ToUniTask().AttachExternalCancellation(ct);
            }
            catch
            {
                handle.Release();
                storeHandle(null);
                throw;
            }

            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                handle.Release();
                storeHandle(null);
                Debug.LogWarning($"[WeaponBase] Load icon '{location}' failure: {error}");
                return null;
            }

            UnityEngine.Object asset = handle.AssetObject;
            if (asset is Sprite sprite)
                return sprite;
            return null;
        }

        private void CancelLoad()
        {
            if (_loadCts == null)
                return;

            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }

        private void ReleaseHandles()
        {
            ReleaseHandle(ref _projectileHandle);
            ReleaseHandle(ref _modelHandle);
            ReleaseHandle(ref _effectHandle);
            ReleaseHandle(ref _iconHandle);
        }

        private static void ReleaseHandle(ref AssetHandle handle)
        {
            if (handle == null)
                return;
            if (handle.IsValid)
                handle.Release();
            handle = null;
        }

        void Update()
        {
            FireDelayTimer -= Time.deltaTime;
            if (FireDelayTimer <= 0)
                FireDelayTimer = 0;

            RecoilTimer -= _config.RecoilSpeed * Time.deltaTime;
            if (RecoilTimer <= 0)
                RecoilTimer = 0;

            // 异步加载未完成时不扣弹、不开火（避免换枪窗口误消耗）
            if (Input_FireHold && _assetsReady)
            {
                if (FireDelayTimer == 0)
                {
                    if (AmmoCount > 0 || _config.InfiniteAmmo)
                    {
                        OnFireFeedback();

                        if (m_OwnerCharacter != null && m_OwnerCharacter.m_WeaponPowerType == 2)
                        {
                            FireWeaponPowered();
                        }
                        else
                        {
                            FireWeapon();
                        }

                        AmmoCount -= 1;

                        if (m_OwnerCharacter != null && m_OwnerCharacter.m_WeaponPowerType > 0)
                        {
                            m_OwnerCharacter.m_WeaponPowerAmmo--;
                        }

                        RecoilTimer = 1;
                    }

                    FireDelayTimer = _config.FireDelay;
                    if (m_OwnerCharacter != null && m_OwnerCharacter.m_WeaponPowerType == 1)
                    {
                        FireDelayTimer = .5f * _config.FireDelay;
                    }
                }
            }

            Input_FireHold = false;
        }

        /// <summary>开火瞬间反馈（镜头抖动等），子类按枪种覆盖并自行调参。</summary>
        protected virtual void OnFireFeedback()
        {
        }

        /// <summary>普通开火入口：音效 + 子类射击逻辑。</summary>
        public void FireWeapon()
        {
            if (!_assetsReady)
                return;

            PlayFireSound();
            OnFire();
        }

        /// <summary>强化开火入口：音效 + 子类强化射击逻辑。</summary>
        public void FireWeaponPowered()
        {
            if (!_assetsReady)
                return;

            PlayFireSound();
            OnFirePowered();
        }

        /// <summary>具体射击逻辑（弹道/动画/枪口特效），子类覆盖实现差异化开火。</summary>
        protected virtual void OnFire()
        {
            if (_projectilePrefab != null)
            {
                m_OwnerCharacter.PlayFireAnimation();
                CreateProjectile(0);
            }

            CreateParticle();
        }

        /// <summary>强化射击逻辑，子类可覆盖。</summary>
        protected virtual void OnFirePowered()
        {
            if (_projectilePrefab != null)
            {
                for (int i = -1; i < 2; i++)
                    CreateProjectile(i * 10);
            }

            CreateParticle();
        }

        protected void PlayFireSound()
        {
            if (_config.FireSound <= 0)
                return;

            Transform bind = WeaponModel != null ? WeaponModel.transform : transform;
            GameFrameWork.Sound?.PlaySound(_config.FireSound, bind);
        }

        public virtual void CreateProjectile(float deltaAngle)
        {
            if (_projectilePrefab == null)
                return;

            Vector3 position = WeaponModel != null ? WeaponModel.m_FirePoint.position : transform.position;
            Vector3 forward = Quaternion.Euler(0, deltaAngle, 0) * m_OwnerCharacter.m_CharBody.m_UpperAimBase.forward;
            GameObject obj = BulletPool.SpawnBullet(_projectilePrefab, position, forward, 5f);
            if (obj == null)
                return;

            ProjectileBase projectile = obj.GetComponent<ProjectileBase>();
            projectile.m_Speed = _config.ProjectileSpeed;
            projectile.m_Creator = m_Owner;
            projectile.m_Damage = _config.ProjectileDamage;
            projectile.m_Range = _config.ProjectileRange;
            // 阵营以持有者为准，避免敌人复用玩家子弹 Prefab 时 m_IsEnemyTeam 为 false
            projectile.m_IsEnemyTeam = m_OwnerCharacter != null && !(m_OwnerCharacter is PlayerCharacter);
        }

        public virtual void CreateParticle()
        {
            if (_effectPrefab != null)
            {
                GameObject obj = Instantiate(_effectPrefab);
                if (WeaponModel != null)
                    obj.transform.SetParent(WeaponModel.m_FirePoint, false);
                else
                    obj.transform.SetParent(transform, false);

                Destroy(obj, 3);
            }
        }

        public void AddModel()
        {
            if (_weaponModelPrefab == null)
                return;

            WeaponModel = Instantiate(_weaponModelPrefab).GetComponent<WeaponModel>();
        }
    }
}
