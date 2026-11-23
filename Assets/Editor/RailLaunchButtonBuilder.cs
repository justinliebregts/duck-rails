using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Compound departure-gate launch button for private staging, plus the wall hint that
/// replaced the old private keypad. Room create/join now lives in the pause menu.
/// </summary>
public static class RailLaunchButtonBuilder
{
    const string PrefabPath = "Assets/DuckRails/PressableButton.prefab";
    const string FontPath = "Assets/Fonts/CutePixel SDF.asset";
    public const string LaunchName = "Private launch", HintName = "Private room hint";
    public const string HintText = "Want to create a private room?\nDo so from the pause menu";
    static readonly string[] Retired = { "Private keypad", "Private terminal", "Private instructions" };

    [MenuItem("Duck Rails/Compound/Install Launch Button")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Save scene edits first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var compound = EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity");
        Install(UnityEngine.Object.FindAnyObjectByType<RailWorld>());
        EditorSceneManager.SaveScene(compound);
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    public static void Install(RailWorld world)
    {
        var parent = world.compound.transform;
        foreach (string name in Retired)
            for (var old = parent.Find(name); old != null; old = parent.Find(name))
                UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach (string name in new[] { LaunchName, HintName })
            for (var old = parent.Find(name); old != null; old = parent.Find(name))
                UnityEngine.Object.DestroyImmediate(old.gameObject);

        // Hint sits on the controls mounting wall where the keypad used to be.
        var hint = Label(parent, HintName, HintText, new Vector3(-4.9f, 1.0f, 8.37f), new Vector2(2.4f, .9f), 2f);
        hint.enableAutoSizing = true; hint.fontSizeMin = .5f; hint.fontSizeMax = 2f;
        hint.textWrappingMode = TextWrappingModes.Normal;

        // Launch control on the departure gate, below the countdown board text.
        var root = new GameObject(LaunchName);
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(0, 0, 8.62f);
        root.transform.rotation = Quaternion.identity;
        var launch = root.AddComponent<RailLaunchButton>();
        launch.prompt = Label(root.transform, "Launch prompt", "Owner, push to start!", new Vector3(0, .8f, 0), new Vector2(3.2f, .35f), 2.4f);
        launch.prompt.enableAutoSizing = true; launch.prompt.fontSizeMin = .5f; launch.prompt.fontSizeMax = 2.4f;
        launch.button = Key(root.transform, "Launch expedition", "LAUNCH\nEXPEDITION", 0, .46f, .9f, .32f);
        InstallPublicTrainPanel(world);
        EditorUtility.SetDirty(launch);
        EditorUtility.SetDirty(world);
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
    }

    public static void InstallPublicTrainPanel(RailWorld world)
    {
        var parent = world.compound.transform;
        const string name = "Public train join";
        var old = parent.Find(name);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(-4.2f, .90f, 8.47f);
        var control = root.AddComponent<RailPublicTrainButton>();
        // Same look as the terminal's PLATFORMS / SUPPLIES / STATION INFO boards: olive painted
        // panel with brown plank trim, textured from the terminal's own Town atlas.
        var board = new GameObject("Sign board", typeof(MeshFilter), typeof(MeshRenderer));
        board.transform.SetParent(root.transform, false);
        board.GetComponent<MeshFilter>().sharedMesh = SignBoardMesh();
        var boardCollider = board.AddComponent<BoxCollider>();
        boardCollider.center = new Vector3(0, 0, BoardBack / 2 + BoardFront / 2);
        boardCollider.size = new Vector3(BoardWidth + TrimOverhang * 2, BoardHeight, BoardBack - TrimFront);
        var boardRenderer = board.GetComponent<MeshRenderer>();
        boardRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TownAtlasPath);
        GameObjectUtility.SetStaticEditorFlags(board, StaticEditorFlags.ContributeGI);
        // Lit from probes so the board needs no lightmap rebake.
        boardRenderer.receiveGI = ReceiveGI.LightProbes;
        control.panel = new GameObject("Panel");
        control.panel.transform.SetParent(root.transform, false);
        board.transform.SetParent(control.panel.transform, false);
        var panel = control.panel.transform;
        control.heading = Label(panel, "Join heading", RailPublicTrainButton.Title, new Vector3(0, .16f, -.033f), new Vector2(1.42f, .40f), 2f);
        control.heading.enableAutoSizing = true;
        control.heading.fontSizeMin = .5f;
        control.heading.fontSizeMax = 2f;
        control.heading.color = SignIvory;
        control.button = Key(panel, "Join train", "JOIN", 0, -.19f, .60f, .23f);
        control.button.transform.localPosition += Vector3.back * .09f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(control.button.transform);
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
    }

    const string BoardMeshPath = "Assets/DuckRails/Compound/SubwayTerminal/Meshes/Public join sign board.asset";
    const string TownAtlasPath = "Assets/DuckRails/Compound/SubwayTerminal/Materials/Subway Town atlas.mat";
    /// <summary>Ivory used by the terminal's raised sign lettering.</summary>
    public static readonly Color SignIvory = new Color(.91f, .867f, .753f);
    const float BoardWidth = 1.60f, BoardHeight = .80f, BoardFront = -.03f, BoardBack = .02f;
    const float TrimHeight = .07f, TrimOverhang = .04f, TrimFront = -.05f;
    // Town atlas is a 4x4 tile sheet: olive painted metal is tile (0,0), brown planks (3,2).
    static readonly Rect OliveTile = new Rect(.004f, .004f, .242f, .242f);
    static readonly Rect PlankTile = new Rect(.754f, .504f, .242f, .242f);
    // Texel density of the terminal signs: one atlas tile spans about 4 m.
    const float TileMetres = 4.1f;

    /// <summary>
    /// Flat olive board with brown plank trim along the top and bottom, matching the
    /// terminal's hanging signs. Box faces only, so it is cheap on Quest.
    /// </summary>
    public static Mesh SignBoardMesh()
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect tile, bool grainAlongX)
        {
            // a-b-c-d clockwise seen from the face's outside (Unity front faces).
            var n = Vector3.Cross(b - a, d - a).normalized;
            int i = verts.Count;
            foreach (var p in new[] { a, b, c, d })
            {
                verts.Add(p);
                // Planar coordinates on the face; planks run along the trim's length.
                Vector2 st = Mathf.Abs(n.z) > .5f ? new Vector2(p.x, p.y) : Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) : new Vector2(p.z, p.y);
                if (grainAlongX) st = new Vector2(st.y, st.x);
                st = st / TileMetres + new Vector2(.5f, .5f);
                uvs.Add(new Vector2(tile.x + Mathf.Repeat(st.x, 1f) * tile.width, tile.y + Mathf.Repeat(st.y, 1f) * tile.height));
            }
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        void Box(Vector3 min, Vector3 max, Rect tile, bool grainAlongX)
        {
            Vector3 P(int x, int y, int z) => new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
            Quad(P(0, 0, 0), P(0, 1, 0), P(1, 1, 0), P(1, 0, 0), tile, grainAlongX); // front (-z)
            Quad(P(1, 0, 1), P(1, 1, 1), P(0, 1, 1), P(0, 0, 1), tile, grainAlongX); // back
            Quad(P(0, 1, 0), P(0, 1, 1), P(1, 1, 1), P(1, 1, 0), tile, grainAlongX); // top
            Quad(P(0, 0, 1), P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), tile, grainAlongX); // bottom
            Quad(P(0, 0, 1), P(0, 1, 1), P(0, 1, 0), P(0, 0, 0), tile, grainAlongX); // left
            Quad(P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1), tile, grainAlongX); // right
        }
        float hw = BoardWidth / 2, hh = BoardHeight / 2;
        Box(new Vector3(-hw, -hh + TrimHeight, BoardFront), new Vector3(hw, hh - TrimHeight, BoardBack), OliveTile, false);
        float tw = hw + TrimOverhang;
        Box(new Vector3(-tw, hh - TrimHeight, TrimFront), new Vector3(tw, hh, BoardBack), PlankTile, true);
        Box(new Vector3(-tw, -hh, TrimFront), new Vector3(tw, -hh + TrimHeight, BoardBack), PlankTile, true);

        var mesh = new Mesh { name = "Public join sign board" };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        Unwrapping.GenerateSecondaryUVSet(mesh);

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(BoardMeshPath);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, BoardMeshPath); return mesh; }
        existing.Clear();
        existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.tangents = mesh.tangents;
        existing.uv = mesh.uv; existing.uv2 = mesh.uv2;
        existing.triangles = mesh.triangles;
        existing.RecalculateBounds();
        existing.UploadMeshData(false);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssetIfDirty(existing);
        return existing;
    }

    static PressableButton Key(Transform parent, string name, string text, float x, float y, float width, float height)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Missing " + PrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name; go.transform.localPosition = new Vector3(x, y, 0);
        var button = go.GetComponent<PressableButton>();
        go.GetComponent<BoxCollider>().size = new Vector3(width, height, .07f);
        button.cap.Find("Cap").localScale = new Vector3(width, height, .05f);
        var label = go.GetComponentInChildren<TMP_Text>();
        label.text = text; label.rectTransform.sizeDelta = new Vector2(width - .04f, height - .04f);
        label.enableAutoSizing = true; label.fontSizeMin = .3f; label.fontSizeMax = 1.4f;
        SetFont(label);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<BoxCollider>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(button.cap.Find("Cap"));
        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        PrefabUtility.RecordPrefabInstancePropertyModifications(label.rectTransform);
        return button;
    }

    static TMP_Text Label(Transform parent, string name, string text, Vector3 localPos, Vector2 size, float fontSize)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
        var label = go.AddComponent<TextMeshPro>(); label.text = text; label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = size;
        label.color = Color.white;
        SetFont(label);
        return label;
    }

    static void SetFont(TMP_Text label)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) label.font = font;
    }
}
