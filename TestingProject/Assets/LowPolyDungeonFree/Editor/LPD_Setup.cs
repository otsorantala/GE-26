// Low Poly Dungeon - one-click setup.
//
// Materials bind automatically on import (each FBX is set to find an
// existing material by name, searching up), and the prefabs ship baked. So
// this tool is for REBUILDING what Unity cannot do from a .meta: turning
// every imported model into a prefab, and laying the library out in a
// scene.
//
//   Tools > Low Poly Dungeon > Build Prefabs
//   Tools > Low Poly Dungeon > Build Library Scene
//
// TODO(demo scenes): furnished demo levels. See the note above
// BuildPrefabs for the prefab side and the one at the end of this class
// for the scene side.
//
// The torch flame: FX_LPD_Fire.prefab is built by BuildFirePrefab() below
// and parented to every LIT prefab by AddFireToLit(), so a buyer who drags
// a torch into a scene gets moving fire and a flickering light without
// wiring anything. Three things the interiors pack learned, all of which
// this honours:
//   * Stop() the system before editing its modules - several refuse writes
//     while playing and the edits are silently dropped.
//   * ALPHA blending, not additive: overlapping additive orange sums to
//     lemon yellow exactly where the flame is densest.
//   * SaveAsPrefabAsset does NOT consume its GameObject. Destroy the
//     authoring object, or it is saved into whatever scene is open next
//     and the validator reports a loose particle system.
//
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LowPolyDungeon
{
    public static class LPD_Setup
    {
        const string Root      = "Assets/LowPolyDungeonFree";
        const string ModelsDir = Root + "/Models";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir  = Root + "/Scenes";
        const string RoomsDir  = Root + "/Demo/Rooms";

        // TODO(demo scenes): assembled demo levels get their own root pair,
        // kept apart so the library folders still hold only library
        // pieces - e.g. { Root + "/Demo/Levels", PrefabDir + "/Levels" }.
        // Add it to Roots below; nothing else in BuildPrefabs changes.
        static readonly string[][] Roots =
        {
            new[] { ModelsDir, PrefabDir },
        };

        [MenuItem("Tools/Low Poly Dungeon/Build Prefabs", false, 10)]
        public static void BuildPrefabs()
        {
            var jobs = new List<KeyValuePair<string, string>>();
            foreach (var pair in Roots)
            {
                if (!AssetDatabase.IsValidFolder(pair[0])) continue;
                foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { pair[0] }))
                {
                    var ap  = AssetDatabase.GUIDToAssetPath(g);
                    var sb  = Path.GetDirectoryName(
                        ap.Substring(pair[0].Length + 1)).Replace("\\", "/");
                    jobs.Add(new KeyValuePair<string, string>(
                        ap, string.IsNullOrEmpty(sb) ? pair[1] : pair[1] + "/" + sb));
                }
            }
            if (jobs.Count == 0)
            {
                Debug.LogWarning("[LPD] No models found under " + ModelsDir);
                return;
            }

            EnsureFolder(PrefabDir);

            // EVERY OUTPUT FOLDER FIRST, BEFORE THE BATCH.
            //
            // StartAssetEditing defers AssetDatabase work until the matching
            // Stop, and that includes CreateFolder. Called inside the batch,
            // EnsureFolder's IsValidFolder check never sees the folder it
            // has just asked for, so it asks again for the next model - and
            // Unity, told to create a folder that already exists on disk,
            // makes "05_Containers 1", then "05_Containers 2". On the
            // interiors pack this produced 2,299 numbered junk folders
            // instead of 22.
            var dirs = new HashSet<string>();
            foreach (var j in jobs) dirs.Add(j.Value);
            foreach (var d in dirs) EnsureFolder(d);
            AssetDatabase.Refresh();

            int made = 0, skipped = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < jobs.Count; i++)
                {
                    var path = jobs[i].Key;
                    var src  = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (src == null) { skipped++; continue; }

                    // The folder structure is mirrored so a buyer can find
                    // the prefab for a model without hunting through every
                    // one of them in one flat directory.
                    var outDir = jobs[i].Value;

                    var outPath = outDir + "/" + Path.GetFileNameWithoutExtension(path) + ".prefab";
                    if (File.Exists(outPath)) { skipped++; continue; }

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    inst.transform.position = Vector3.zero;
                    inst.transform.rotation = Quaternion.identity;

                    // Unpack before saving: saving a prefab INSTANCE of the
                    // FBX writes a variant whose every property is an
                    // override of the model, and the buyer cannot then edit
                    // it without the changes fighting the importer.
                    PrefabUtility.UnpackPrefabInstance(
                        inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                    foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                    {
                        var mc = mf.gameObject.GetComponent<MeshCollider>();
                        if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }

                    PrefabUtility.SaveAsPrefabAsset(inst, outPath);
                    Object.DestroyImmediate(inst);
                    made++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // A folder count that is not the category count means the
            // deferred-AssetDatabase trap above has come back.
            var folders = AssetDatabase.GetSubFolders(PrefabDir);
            Debug.Log($"[LPD] Prefabs: {made} created, {skipped} already "
                    + $"present, in {folders.Length} folders.");
            if (folders.Length > dirs.Count)
                Debug.LogError($"[LPD] {folders.Length} prefab folders but "
                             + $"only {dirs.Count} categories - duplicates "
                             + "were created.");

            // Every lit fitting gets moving fire and a flickering light, so
            // the buyer does not drag a torch in and find a still picture of
            // one. FX folder is excluded from the model count on purpose -
            // a particle system is not a model.
            AddFireToLit(PrefabMap());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Low Poly Dungeon/Build Library Scene", false, 12)]
        public static void BuildLibraryScene()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[LPD] Build Prefabs first.");
                return;
            }
            EnsureFolder(SceneDir);
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var byCat = new SortedDictionary<string, List<GameObject>>();
            foreach (var g in guids)
            {
                var p  = AssetDatabase.GUIDToAssetPath(g);
                // TODO(demo scenes / torch flame): skip Prefabs/Levels and
                // Prefabs/FX here once they exist - the library scene lays
                // out the PIECES, and a whole level dropped into the parts
                // grid buries the row it lands on.
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) continue;
                // "SM_LPD_Crate_ChestIronbound_01" -> "Crate"
                var parts = go.name.Split('_');
                var cat   = parts.Length > 2 ? parts[2] : "Misc";
                if (!byCat.TryGetValue(cat, out var list))
                    byCat[cat] = list = new List<GameObject>();
                list.Add(go);
            }

            float z = 0f;
            foreach (var kv in byCat)
            {
                var row    = new GameObject("LPD_" + kv.Key);
                row.transform.position = new Vector3(0f, 0f, z);
                float x    = 0f;
                float depth = 0f;
                foreach (var prefab in kv.Value.OrderBy(o => o.name))
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.transform.SetParent(row.transform, false);

                    var b = LocalBounds(inst);
                    inst.transform.localPosition = new Vector3(x + b.size.x * 0.5f, 0f, 0f);
                    x     += b.size.x + 0.5f;
                    depth  = Mathf.Max(depth, b.size.z);
                }
                z += depth + 1.5f;
            }

            var scenePath = SceneDir + "/LPD_Library.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            // Saved explicitly. A scene that is built but never written is a
            // scene the buyer opens to find empty.
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[LPD] Library scene written to {scenePath}");
        }

        /// Turn each assembled room FBX into a shipped demo scene.
        ///
        /// Built because the package had NO .unity file in it at all - 168
        /// assets, every one a model, prefab, material or texture, and not
        /// one room a buyer could open. RELEASE_REQUIREMENTS.md makes demo
        /// scenes mandatory and says why: a kit of loose models does not
        /// show a buyer what it is for.
        ///
        /// The layout arrives as an FBX because the FBX importer already
        /// does the Blender-to-Unity axis conversion; redoing that by hand
        /// in C# is how a mapping comes to look right and be silently
        /// wrong. Every welded child is then replaced by a LINKED PREFAB
        /// INSTANCE at that child's own world transform, so the buyer can
        /// move a torch or swap a door instead of inheriting one frozen
        /// mesh.
        [MenuItem("Tools/Low Poly Dungeon/Build Demo Scenes", false, 13)]
        public static void BuildDemoScenes()
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { RoomsDir });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[LPD] No room models under " + RoomsDir);
                return;
            }
            EnsureFolder(SceneDir);

            var prefabs = PrefabMap();
            if (prefabs.Count == 0)
                Debug.LogWarning("[LPD] No prefabs found - run Build Prefabs "
                               + "first, or the demo scenes will be welded "
                               + "meshes you cannot take apart.");

            int made = 0, swapped = 0, kept = 0;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var src  = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) continue;

                var scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var name = Path.GetFileNameWithoutExtension(path)
                               .Replace("SM_LPD_Room_", "");

                var room = (GameObject)PrefabUtility.InstantiatePrefab(src);
                room.transform.position = Vector3.zero;
                room.transform.rotation = Quaternion.identity;
                PrefabUtility.UnpackPrefabInstance(
                    room, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

                var root = new GameObject(name);
                int s2 = 0, k2 = 0;
                Modularise(room, root.transform, prefabs, ref s2, ref k2);
                Object.DestroyImmediate(room);
                swapped += s2;
                kept += k2;

                AddSceneLighting(root);

                var scenePath = SceneDir + "/Demo_" + name + ".unity";
                EditorSceneManager.MarkSceneDirty(scene);
                // Saved explicitly. A scene that is built but never written
                // is a scene the buyer opens to find empty.
                EditorSceneManager.SaveScene(scene, scenePath);
                made++;
                Debug.Log($"[LPD] Demo_{name}: {s2} prefab instances, "
                        + $"{k2} room-specific meshes");
            }
            AssetDatabase.Refresh();
            Debug.Log($"[LPD] {made} demo scene(s) written to {SceneDir}"
                    + $" - {swapped} prefab instances, {kept} shell meshes.");
        }

        // ------------------------------------------------------------ fire
        //
        // The pack's flames are MESHES with an emissive material, which is
        // right for a thumbnail and wrong for a game: a torch that does not
        // move reads as a lamp. So every LIT prefab also carries a particle
        // system and a flickering point light, wired here rather than left
        // to the buyer.

        const string FxDir = Root + "/Prefabs/FX";
        const string FirePath = FxDir + "/FX_LPD_Fire.prefab";

        /// The warm-orange flame particle, built once and reused.
        static GameObject BuildFirePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FirePath);
            if (existing != null) return existing;
            EnsureFolder(FxDir);

            var go = new GameObject("FX_LPD_Fire");
            var ps = go.AddComponent<ParticleSystem>();
            // STOP FIRST. Several modules refuse writes while the system is
            // playing and drop the edit silently.
            ps.Stop();

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.17f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.00f, 0.62f, 0.18f), new Color(1.00f, 0.86f, 0.42f));
            main.gravityModifier = -0.06f;      // heat rises
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            var em = ps.emission;
            em.rateOverTime = 22f;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 12f;
            sh.radius = 0.045f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.78f, 0.35f), 0f),
                        new GradientColorKey(new Color(0.95f, 0.36f, 0.08f), 0.6f),
                        new GradientColorKey(new Color(0.35f, 0.10f, 0.03f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.18f),
                        new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 0.05f));

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.alignment = ParticleSystemRenderSpace.View;
            // ALPHA, not additive. Overlapping additive orange sums to lemon
            // yellow exactly where the flame is densest and it stops reading
            // as fire.
            var mat = new Material(Shader.Find("Particles/Standard Unlit")
                                   ?? Shader.Find("Sprites/Default"));
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                Root + "/Textures/LPD_Particle.png");
            if (tex != null) mat.mainTexture = tex;
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2f);   // fade
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f); // alpha
            AssetDatabase.CreateAsset(mat, FxDir + "/M_LPD_Fire.mat");
            rend.sharedMaterial = mat;

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.68f, 0.34f);
            l.intensity = 1.6f;
            l.range = 6.5f;
            l.shadows = LightShadows.None;
            lightGo.AddComponent<LPD_Flicker>();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, FirePath);
            // SaveAsPrefabAsset does NOT consume its GameObject. Left alive it
            // is saved into whatever scene opens next and the validator
            // reports a loose particle system.
            Object.DestroyImmediate(go);
            Debug.Log("[LPD] built " + FirePath);
            return saved;
        }

        /// Which atlas cells mean FIRE, and which mean magic. A glowing
        /// runestone must not sprout an orange flame.
        ///
        /// WHY CELLS AND NOT MATERIAL NAMES. v1.0.0 asked each material
        /// whether its NAME contained "EMIT_TORCH" and so on. Those names
        /// are Kiln-side: one material per swatch while the model is being
        /// authored. The pack ships THREE materials, and by the time a model
        /// is in Unity every emissive face is on M_LPD_Emissive, with the
        /// swatch surviving only in the UV - pinned to the centre of one
        /// atlas cell. So the test could never be true: it was false for all
        /// 811 prefabs, AddFireToLit attached nothing to anything, and the
        /// pack shipped claiming animated fire with no fire on any torch.
        /// A buyer found it. Measured on the shipped .unitypackage: 811
        /// prefabs, 0 references to FX_LPD_Fire, 0 of 11 demo scenes.
        ///
        /// The comment it replaced was right that the name lies and the
        /// swatch is the truth. It just read the swatch off the wrong thing.
        static readonly int[] FireCells = { 120, 121, 122, 127, 128, 129, 130, 131 };
        const int AtlasCols = 16, AtlasRows = 16;

        /// Does this UV sit in a fire swatch's cell? lp_atlas pins every face
        /// to the CENTRE of its cell: u = (col + 0.5) / cols and
        /// v = 1 - (row + 0.5) / rows, so the cell is recoverable by floor.
        static bool IsFireUV(Vector2 uv)
        {
            int col = Mathf.FloorToInt(uv.x * AtlasCols);
            int row = Mathf.FloorToInt((1f - uv.y) * AtlasRows);
            if (col < 0 || col >= AtlasCols || row < 0 || row >= AtlasRows)
                return false;
            int idx = row * AtlasCols + col;
            foreach (var c in FireCells) if (c == idx) return true;
            return false;
        }

        /// Does this prefab actually carry fire? Answered from the atlas
        /// cells its vertices sample, which is what "the swatch IS the
        /// flame" means once the model is in Unity.
        ///
        /// Name matching was tried first and is not coming back: measured
        /// against the built library, matching "Lit" or "Candle" caught 10
        /// of the 51 emissive models and missed 41 - every lantern, every
        /// candelabra, both oil lamps, both chandeliers, the hearths, the
        /// censers, the flame vent and the branding iron.
        static bool HasFire(GameObject root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var uv = mesh.uv;
                if (uv == null) continue;
                for (int i = 0; i < uv.Length; i++)
                    if (IsFireUV(uv[i])) return true;
            }
            return false;
        }

        /// Parent a flame to every prefab that is actually alight.
        static int AddFireToLit(Dictionary<string, GameObject> prefabs)
        {
            var fire = BuildFirePrefab();
            if (fire == null) return 0;
            int n = 0, seen = 0;
            foreach (var kv in prefabs)
            {
                var target = kv.Value;
                if (target == null) continue;
                var path = AssetDatabase.GetAssetPath(target);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (!HasFire(root)) { PrefabUtility.UnloadPrefabContents(root); continue; }
                seen++;
                if (root.transform.Find("FX_LPD_Fire") == null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(fire, root.transform);
                    // Sit the flame where the emissive geometry actually is,
                    // not at the top of the whole fitting - a chandelier's
                    // candles are at its rim, a hearth's fire is in its bed.
                    var b = EmissiveBounds(root);
                    inst.transform.position = b.center + Vector3.up * (b.extents.y * 0.35f);
                    n++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[LPD] fire + flicker light added to {n} prefabs "
                    + $"({seen} carry an emissive flame material)");
            return n;
        }

        /// The bounds of just the EMISSIVE geometry - the flame itself.
        /// Where the flame goes: the bounds of the FIRE VERTICES themselves.
        ///
        /// This carried the same material-name bug as HasFire, and fixing it
        /// the same way makes it strictly better than it was meant to be.
        /// The old version took the whole RENDERER's bounds once a material
        /// matched, which for a chandelier is the entire fitting; per-vertex
        /// bounds put the flame on the candles.
        static Bounds EmissiveBounds(GameObject root)
        {
            bool any = false;
            var b = new Bounds(root.transform.position, Vector3.zero);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var uv = mesh.uv;
                var vt = mesh.vertices;
                if (uv == null || vt == null || uv.Length != vt.Length) continue;
                for (int i = 0; i < vt.Length; i++)
                {
                    if (!IsFireUV(uv[i])) continue;
                    var p = mf.transform.TransformPoint(vt[i]);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return any ? b : LocalBounds(root);
        }

        /// Every prefab in the pack, by model name.
        static Dictionary<string, GameObject> PrefabMap()
        {
            var map = new Dictionary<string, GameObject>();
            if (!AssetDatabase.IsValidFolder(PrefabDir)) return map;
            foreach (var g in AssetDatabase.FindAssets("t:Prefab",
                                                       new[] { PrefabDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var n = Path.GetFileNameWithoutExtension(p);
                if (!map.ContainsKey(n))
                    map[n] = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return map;
        }

        /// Blender names duplicates "Foo.001", and Unity keeps that in the
        /// FBX hierarchy, so the name has to be reduced to the model it is a
        /// copy of before it can be matched to a prefab. A demo room is
        /// mostly copies, so getting this wrong leaves the whole scene
        /// welded.
        static string ModelName(string n)
        {
            n = n.Trim();
            int dot = n.LastIndexOf('.');
            if (dot > 0 && n.Length - dot >= 2)
            {
                bool digits = true;
                for (int i = dot + 1; i < n.Length; i++)
                    if (!char.IsDigit(n[i])) { digits = false; break; }
                if (digits) n = n.Substring(0, dot);
            }
            return n;
        }

        /// Which Prefabs/ subfolder a model belongs to, from its name.
        static string CategoryOf(string model)
        {
            var parts = model.Split('_');
            return parts.Length > 2 ? parts[2] : "Misc";
        }

        /// Replace each welded child with a linked prefab instance.
        static void Modularise(GameObject room, Transform root,
                               Dictionary<string, GameObject> prefabs,
                               ref int swapped, ref int kept)
        {
            var groups = new Dictionary<string, Transform>();
            Transform Group(string cat)
            {
                if (!groups.TryGetValue(cat, out var t))
                {
                    var go = new GameObject(cat);
                    go.transform.SetParent(root, false);
                    t = go.transform;
                    groups[cat] = t;
                }
                return t;
            }

            var children = new List<Transform>();
            foreach (Transform c in room.transform) children.Add(c);

            foreach (var child in children)
            {
                var model = ModelName(child.name);
                if (!prefabs.TryGetValue(model, out var prefab) || prefab == null)
                {
                    child.SetParent(Group("Shell"), true);
                    kept++;
                    continue;
                }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.name = child.name;
                inst.transform.SetParent(Group(CategoryOf(model)), false);
                inst.transform.SetPositionAndRotation(
                    child.position, child.rotation);
                inst.transform.localScale = child.lossyScale;
                Object.DestroyImmediate(child.gameObject);
                swapped++;
            }
        }

        /// A dungeon is dark, so a demo scene with no lights opens black and
        /// reads as broken. One dim ambient fill plus a point light at every
        /// emissive fitting the room already contains.
        static void AddSceneLighting(GameObject root)
        {
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.10f, 0.13f);

            var sunGo = new GameObject("Fill Light");
            sunGo.transform.SetParent(root.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(52f, 154f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.62f, 0.66f, 0.85f);
            sun.intensity = 0.22f;
            sun.shadows = LightShadows.Soft;

            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var n = r.gameObject.name;
                if (n.IndexOf("Lit", System.StringComparison.Ordinal) < 0 &&
                    n.IndexOf("Candle", System.StringComparison.Ordinal) < 0 &&
                    n.IndexOf("Glow", System.StringComparison.Ordinal) < 0)
                    continue;
                var go = new GameObject("PointLight");
                go.transform.SetParent(r.transform, false);
                go.transform.position = r.bounds.center + Vector3.up * 0.15f;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.70f, 0.38f);
                l.intensity = 1.9f;
                l.range = 7.5f;
                l.shadows = LightShadows.None;
            }
        }

        static Bounds LocalBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.5f);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf   = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
