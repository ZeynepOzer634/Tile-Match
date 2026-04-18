using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using TileMatch.Data;

namespace TileMatch.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        // --- Settings ---
        private LevelData targetLevelData;
        private const int GRID_MIN = -5;
        private const int GRID_MAX = 5;
        private const int GRID_SIZE = GRID_MAX - GRID_MIN + 1; // 11x11
        private int currentLayer = 0;    // Z katmanı
        private int currentTileType = 0; // Hangi meyve/ikon
        private int maxLayers = 5;

        // Tile sprites for preview (BoardManager'daki ile aynı)
        private Sprite[] tileSprites;
        private SerializedObject serializedBoardManager;

        // Grid data: key = Vector3Int(x, y, z), value = tileTypeId
        private Dictionary<Vector3Int, int> gridData = new Dictionary<Vector3Int, int>();

        private Vector2 mainScrollPos;
        private Vector2 scrollPos;
        private Vector2 orderScrollPos;
        private float cellSize = 40f;
        private string[] tileTypeNames;

        // Order data
        private List<int[]> orderList = new List<int[]>();
        
        // Generator setting
        private int generateOrdersCount = 10;

        [MenuItem("TileMatch/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(500, 600);
        }

        private void OnEnable()
        {
            RefreshTileSprites();
        }

        private void RefreshTileSprites()
        {
            // BoardManager'dan sprite listesini bul
            var boardManager = FindFirstObjectByType<Board.BoardManager>();
            if (boardManager != null)
            {
                var so = new SerializedObject(boardManager);
                var prop = so.FindProperty("tileSprites");
                if (prop != null && prop.isArray)
                {
                    tileSprites = new Sprite[prop.arraySize];
                    tileTypeNames = new string[prop.arraySize];
                    for (int i = 0; i < prop.arraySize; i++)
                    {
                        tileSprites[i] = prop.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                        tileTypeNames[i] = tileSprites[i] != null ? $"{i}: {tileSprites[i].name}" : $"{i}: (empty)";
                    }
                }
            }

            if (tileTypeNames == null || tileTypeNames.Length == 0)
            {
                tileTypeNames = new string[] { "0", "1", "2", "3", "4" };
            }
        }

        private void OnGUI()
        {
            mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🧩 Tile Match Level Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // --- Target LevelData ---
            targetLevelData = (LevelData)EditorGUILayout.ObjectField("Level Data", targetLevelData, typeof(LevelData), false);

            if (targetLevelData == null)
            {
                EditorGUILayout.HelpBox("Düzenlemek istediğin LevelData dosyasını yukarıya sürükle.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // --- Grid Bilgisi ---
            EditorGUILayout.LabelField($"Grid: X [{GRID_MIN} → {GRID_MAX}]  Y [{GRID_MIN} → {GRID_MAX}]", EditorStyles.miniLabel);
            cellSize = EditorGUILayout.Slider("Hücre Boyutu", cellSize, 20f, 80f);

            EditorGUILayout.Space(5);

            // --- Katman Seçimi ---
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Katman (Z):", GUILayout.Width(80));
            for (int z = 0; z < maxLayers; z++)
            {
                GUI.backgroundColor = (z == currentLayer) ? Color.cyan : Color.white;
                if (GUILayout.Button($"Z={z}", GUILayout.Width(50)))
                {
                    currentLayer = z;
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // --- Taş Türü Seçimi ---
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Taş Türü:", GUILayout.Width(80));
            if (tileTypeNames != null && tileTypeNames.Length > 0)
            {
                currentTileType = EditorGUILayout.Popup(currentTileType, tileTypeNames);
            }
            else
            {
                currentTileType = EditorGUILayout.IntField(currentTileType);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // --- Grid Çizimi ---
            DrawGrid();

            EditorGUILayout.Space(10);

            // --- Butonlar ---
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("💾 LevelData'ya Kaydet", GUILayout.Height(35)))
            {
                SaveToLevelData();
            }

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("📂 LevelData'dan Yükle", GUILayout.Height(35)))
            {
                LoadFromLevelData();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("🗑 Tümünü Temizle", GUILayout.Height(35)))
            {
                if (EditorUtility.DisplayDialog("Temizle", "Tüm taşları silmek istediğine emin misin?", "Evet", "İptal"))
                {
                    gridData.Clear();
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // --- Otonom Level Üretici ---
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🎲 Otonom Level Üretici", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            generateOrdersCount = EditorGUILayout.IntSlider("Sipariş Sayısı (x3 Taş)", generateOrdersCount, 5, 50);
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Üret!", GUILayout.Width(60), GUILayout.Height(20)))
            {
                GenerateAutonomousLevel();
            }
            GUI.backgroundColor = new Color(0.8f, 0.5f, 1f); // Purple
            if (GUILayout.Button("Doğrula", GUILayout.Width(60), GUILayout.Height(20)))
            {
                ValidateCurrentLevel();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // --- İstatistik ---
            EditorGUILayout.Space(5);
            int totalTiles = gridData.Count;
            int tilesOnLayer = 0;
            foreach (var kvp in gridData)
            {
                if (kvp.Key.z == currentLayer) tilesOnLayer++;
            }
            EditorGUILayout.LabelField($"Toplam Taş: {totalTiles} | Bu Katmanda (Z={currentLayer}): {tilesOnLayer}");

            EditorGUILayout.Space(15);

            // ==========================================
            // SIPARIŞ (ORDER) EDİTÖRÜ
            // ==========================================
            DrawOrderEditor();

            EditorGUILayout.EndScrollView();
        }

        private void DrawGrid()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(Mathf.Min(GRID_SIZE * cellSize + 30, 400)));

            Rect gridArea = GUILayoutUtility.GetRect(GRID_SIZE * cellSize, GRID_SIZE * cellSize);

            // Arka plan
            EditorGUI.DrawRect(gridArea, new Color(0.15f, 0.15f, 0.15f));

            for (int y = GRID_MAX; y >= GRID_MIN; y--)
            {
                for (int x = GRID_MIN; x <= GRID_MAX; x++)
                {
                    float xPos = gridArea.x + (x - GRID_MIN) * cellSize;
                    float yPos = gridArea.y + (GRID_MAX - y) * cellSize;
                    Rect cellRect = new Rect(xPos, yPos, cellSize - 2, cellSize - 2);

                    Vector3Int coord = new Vector3Int(x, y, currentLayer);
                    bool hasTile = gridData.ContainsKey(coord);

                    // Hücre rengi
                    Color cellColor;
                    if (hasTile)
                    {
                        // Taş türüne göre renk ver
                        float hue = (gridData[coord] * 0.15f) % 1f;
                        cellColor = Color.HSVToRGB(hue, 0.6f, 0.9f);
                    }
                    else
                    {
                        // Alt katmanlarda taş var mı kontrol et (gölge olarak göster)
                        bool hasBelow = false;
                        for (int z = currentLayer - 1; z >= 0; z--)
                        {
                            if (gridData.ContainsKey(new Vector3Int(x, y, z)))
                            {
                                hasBelow = true;
                                break;
                            }
                        }
                        cellColor = hasBelow ? new Color(0.3f, 0.3f, 0.35f) : new Color(0.22f, 0.22f, 0.22f);
                    }

                    EditorGUI.DrawRect(cellRect, cellColor);

                    // Taş varsa ikon veya ID göster
                    if (hasTile)
                    {
                        int typeId = gridData[coord];
                        if (tileSprites != null && typeId < tileSprites.Length && tileSprites[typeId] != null)
                        {
                            Texture2D tex = AssetPreview.GetAssetPreview(tileSprites[typeId]);
                            if (tex != null)
                            {
                                Rect iconRect = new Rect(cellRect.x + 4, cellRect.y + 4, cellRect.width - 8, cellRect.height - 8);
                                GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                            }
                        }
                        else
                        {
                            GUI.Label(cellRect, typeId.ToString(), new GUIStyle(EditorStyles.boldLabel)
                            {
                                alignment = TextAnchor.MiddleCenter,
                                normal = { textColor = Color.white }
                            });
                        }
                    }

                    // Tıklama: Sol tuş = taş koy, Sağ tuş = taş sil
                    if (Event.current.type == EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.button == 0) // Sol tık: Koy
                        {
                            gridData[coord] = currentTileType;
                            Event.current.Use();
                            Repaint();
                        }
                        else if (Event.current.button == 1) // Sağ tık: Sil
                        {
                            gridData.Remove(coord);
                            Event.current.Use();
                            Repaint();
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void SaveToLevelData()
        {
            if (targetLevelData == null) return;

            Undo.RecordObject(targetLevelData, "Save Level Data");

            targetLevelData.initialTiles.Clear();

            foreach (var kvp in gridData)
            {
                var placement = new LevelData.TilePlacement
                {
                    x = kvp.Key.x,
                    y = kvp.Key.y,
                    z = kvp.Key.z,
                    tileTypeId = kvp.Value
                };
                targetLevelData.initialTiles.Add(placement);
            }

            SaveOrders();

            EditorUtility.SetDirty(targetLevelData);
            AssetDatabase.SaveAssets();
            Debug.Log($"✅ LevelData kaydedildi! Toplam {gridData.Count} taş, {orderList.Count} sipariş.");
        }

        private void LoadFromLevelData()
        {
            if (targetLevelData == null) return;

            gridData.Clear();

            foreach (var placement in targetLevelData.initialTiles)
            {
                Vector3Int coord = new Vector3Int(placement.x, placement.y, placement.z);
                gridData[coord] = placement.tileTypeId;
            }

            LoadOrders();

            Debug.Log($"📂 LevelData yüklendi! Toplam {gridData.Count} taş, {orderList.Count} sipariş.");
            Repaint();
        }

        // ==========================================
        // ORDER EDITOR
        // ==========================================

        private void DrawOrderEditor()
        {
            EditorGUILayout.LabelField("📋 Sipariş Editörü", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            orderScrollPos = EditorGUILayout.BeginScrollView(orderScrollPos, GUILayout.Height(200));

            int removeIndex = -1;

            for (int i = 0; i < orderList.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(30));

                // 3 slot göster
                for (int s = 0; s < 3; s++)
                {
                    // Dropdown ile tür seçimi
                    if (tileTypeNames != null && tileTypeNames.Length > 0)
                    {
                        orderList[i][s] = EditorGUILayout.Popup(orderList[i][s], tileTypeNames, GUILayout.Width(100));
                    }
                    else
                    {
                        orderList[i][s] = EditorGUILayout.IntField(orderList[i][s], GUILayout.Width(30));
                    }
                }

                // Sil butonu
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(20)))
                {
                    removeIndex = i;
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                orderList.RemoveAt(removeIndex);
            }

            EditorGUILayout.EndScrollView();

            // Yeni sipariş ekle butonu
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("+ Yeni Sipariş Ekle", GUILayout.Height(28)))
            {
                orderList.Add(new int[] { 0, 0, 0 });
            }
            GUI.backgroundColor = Color.white;
        }

        // ==========================================
        // SAVE / LOAD (Orders dahil)
        // ==========================================

        private void SaveOrders()
        {
            targetLevelData.levelOrders.Clear();
            foreach (var order in orderList)
            {
                var seq = new LevelData.OrderSequence();
                seq.requiredTileTypeIds = new int[] { order[0], order[1], order[2] };
                targetLevelData.levelOrders.Add(seq);
            }
        }

        private void LoadOrders()
        {
            orderList.Clear();
            foreach (var order in targetLevelData.levelOrders)
            {
                orderList.Add(new int[] {
                    order.requiredTileTypeIds[0],
                    order.requiredTileTypeIds[1],
                    order.requiredTileTypeIds[2]
                });
            }
        }

        private void GenerateAutonomousLevel()
        {
            int numTileTypes = tileTypeNames != null ? tileTypeNames.Length : 5;
            var result = LevelGenerator.Generate(generateOrdersCount, numTileTypes);
            
            if (result.Success)
            {
                gridData = result.GridData;
                orderList = result.Orders;
                Debug.Log($"✅ Otonom Level başarıyla üretildi! Toplam {generateOrdersCount} sipariş, {gridData.Count} taş.");
                Repaint();
            }
            else
            {
                Debug.LogError("❌ Otonom Level üretilemedi. (Tasarım çok sıkışık olabilir, tekrar denenebilir)");
            }
        }

        private void ValidateCurrentLevel()
        {
            if (gridData.Count == 0 || orderList.Count == 0)
            {
                EditorUtility.DisplayDialog("Hata", "Doğrulanacak bir seviye yok (Taş veya Sipariş eksik).", "Tamam");
                return;
            }
            
            bool isSolvable = LevelGenerator.ValidateLevel(gridData, orderList);
            if (isSolvable)
            {
                EditorUtility.DisplayDialog("Doğrulama Başarılı!", "✅ Bu seviye kesinlikle bitirilebilir (çözülebilir) durumdadır.", "Harika");
            }
            else
            {
                EditorUtility.DisplayDialog("Doğrulama Başarısız", "❌ Bu seviye mevcut haliyle bitirilemez. Ya rack (tepsi) doluyor ya da siparişler tahtadaki taşlarla eşleşmiyor.", "Kapat");
            }
        }
    }
}
