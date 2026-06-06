using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace STG.CurveDash
{
    // TEMPORARY diagnostic: auto-runs on Play, waits for the scene to spawn, dumps player/board
    // visibility AND directly tests Addressables loading to a text file. Delete once resolved.
    public static class ModelVisibilityDiag
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            var go = new GameObject("~ModelVisibilityDiag");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<DiagRunner>();
        }

        private class DiagRunner : MonoBehaviour
        {
            private const string CharAddr = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Modular Character/GanzSe Free Modular Character Update 1_1.prefab";
            private const string SkinAddr = "Tile 1";

            private System.Collections.IEnumerator Start()
            {
                yield return new WaitForSecondsRealtime(6f);

                var sb = new StringBuilder();
                sb.AppendLine($"=== ModelVisibilityDiag @ t={Time.time:F1} ===");

                // --- Addressables init ---
                var initH = Addressables.InitializeAsync(false);
                yield return initH;
                sb.AppendLine($"InitializeAsync status={initH.Status}");

                // --- Direct load test: character ---
                yield return TestLoad(sb, "CHARACTER", CharAddr);
                // --- Direct load test: block skin ---
                yield return TestLoad(sb, "SKIN", SkinAddr);

                // --- Runtime renderer state ---
                DumpTree(sb, "Player(Clone)", GameObject.Find("Player(Clone)"), 6);
                DumpTree(sb, "ObjectsPool", GameObject.Find("ObjectsPool"), 4);

                string path = System.IO.Path.Combine(Application.dataPath, "..", "model_diag.txt");
                try { System.IO.File.WriteAllText(path, sb.ToString()); } catch (System.Exception e) { Debug.LogError(e); }
                Debug.Log($"<color=magenta>[ModelVisibilityDiag] WROTE\n{sb}</color>");
            }

            private System.Collections.IEnumerator TestLoad(StringBuilder sb, string label, string addr)
            {
                AsyncOperationHandle<GameObject> h = default;
                bool started = false;
                try { h = Addressables.LoadAssetAsync<GameObject>(addr); started = true; }
                catch (System.Exception e) { sb.AppendLine($"{label} LoadAssetAsync THREW: {e.Message}"); }

                if (!started) yield break;
                yield return h;
                sb.AppendLine($"{label} load '{addr}' → status={h.Status} result={(h.Result != null ? h.Result.name : "NULL")} " +
                              $"opException={(h.OperationException != null ? h.OperationException.Message : "none")}");
            }

            private static void DumpTree(StringBuilder sb, string label, GameObject go, int maxRenderers)
            {
                sb.AppendLine($"\n--- {label} ---");
                if (go == null) { sb.AppendLine("NOT FOUND"); return; }
                var rends = go.GetComponentsInChildren<Renderer>(true);
                int vis = rends.Count(r => r != null && r.enabled && r.gameObject.activeInHierarchy);
                sb.AppendLine($"renderers={rends.Length} enabled&active={vis}");
                foreach (var r in rends.Take(maxRenderers))
                    if (r != null) sb.AppendLine($"  '{r.gameObject.name}' en={r.enabled} act={r.gameObject.activeInHierarchy}");
            }
        }
    }
}
