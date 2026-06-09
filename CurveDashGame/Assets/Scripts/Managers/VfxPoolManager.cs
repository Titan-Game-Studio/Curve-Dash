using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    public class VfxPoolManager : MonoBehaviour
    {
        private static VfxPoolManager _instance;
        public static VfxPoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var obj = new GameObject("[VfxPoolManager]");
                    _instance = obj.AddComponent<VfxPoolManager>();
                    DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        private readonly Dictionary<GameObject, List<GameObject>> _pools = new Dictionary<GameObject, List<GameObject>>();

        /// <summary>
        /// Sinh ra hiệu ứng từ Pool tại vị trí world. Tự động tái sử dụng nếu đã tồn tại các thực thể nhàn rỗi.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
            => SpawnInternal(prefab, position, rotation, null, false);

        /// <summary>
        /// Sinh hiệu ứng và gắn (parent) vào <paramref name="parent"/> với toạ độ/ xoay cục bộ — hiệu ứng sẽ
        /// đi theo parent (vd: aura sinh dưới chân và bám theo nhân vật). Vẫn tái sử dụng từ Pool như bình thường.
        /// </summary>
        public GameObject SpawnAttached(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation)
            => SpawnInternal(prefab, localPosition, localRotation, parent, true);

        private GameObject SpawnInternal(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, bool useLocal)
        {
            if (prefab == null) return null;

            if (!_pools.TryGetValue(prefab, out var list))
            {
                list = new List<GameObject>();
                _pools[prefab] = list;
            }

            GameObject instance = null;

            // Quét qua danh sách để lấy ra đối tượng đang Deactive
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                    continue;
                }
                if (!list[i].activeSelf)
                {
                    instance = list[i];
                    break;
                }
            }

            // Nếu không có đối tượng rảnh, tiến hành Instantiate mới
            if (instance == null)
            {
                instance = Instantiate(prefab);
                list.Add(instance);

                // Gắn helper tự động Deactive khi Particle phát xong
                if (instance.GetComponent<VfxPoolHelper>() == null)
                {
                    instance.AddComponent<VfxPoolHelper>();
                }
            }

            // Đặt parent + toạ độ. Khi có parent, hiệu ứng bám theo parent; khi không, trả về world-space.
            var t = instance.transform;
            if (parent != null)
            {
                t.SetParent(parent, false);
                if (useLocal) { t.localPosition = position; t.localRotation = rotation; }
                else { t.position = position; t.rotation = rotation; }
            }
            else
            {
                if (t.parent != null) t.SetParent(null, false); // gỡ khỏi parent cũ (vd: lần trước bám theo nhân vật)
                t.position = position;
                t.rotation = rotation;
            }

            instance.SetActive(true);

            // Reset và chạy lại toàn bộ Particle System con
            var particles = instance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Clear();
                ps.Play();
            }

            return instance;
        }
    }

    /// <summary>
    /// Giúp tự động theo dõi và ẩn (deactivate) đối tượng khi hiệu ứng Particle System kết thúc.
    /// Tránh việc gọi Destroy sinh rác RAM (Garbage Collection).
    /// </summary>
    public class VfxPoolHelper : MonoBehaviour
    {
        private ParticleSystem[] _particleSystems;
        private float _maxDuration = 2.0f;
        private float _timer = 0f;

        private void Awake()
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in _particleSystems)
            {
                var main = ps.main;
                
                // Tránh việc Particle System tự hủy
                main.stopAction = ParticleSystemStopAction.None;
                
                // Tính toán thời gian phát lâu nhất của hiệu ứng để tự động thu hồi dự phòng
                float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                if (duration > _maxDuration)
                {
                    _maxDuration = duration;
                }
            }
        }

        private void OnEnable()
        {
            _timer = 0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            bool isPlaying = false;
            foreach (var ps in _particleSystems)
            {
                if (ps != null && ps.IsAlive(true))
                {
                    isPlaying = true;
                    break;
                }
            }

            // Nếu các hiệu ứng đã phát xong hoặc hết thời gian tối đa -> Thu hồi về Pool
            if (!isPlaying || _timer >= _maxDuration)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
