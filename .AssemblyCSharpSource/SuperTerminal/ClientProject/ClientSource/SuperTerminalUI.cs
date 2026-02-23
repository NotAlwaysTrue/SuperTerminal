using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace SuperTerminalMain
{
    public partial class SuperTerminal : ItemComponent
    {
        private static GUIFrame mainFrame;
        private static GUIListBox itemList;
        private static GUIFrame leftHolder, rightHolder;
        private static string currentSearch = "";
        private static string selectedCategory = "全部";
        private static Item currentTerminalItem;
        public static void Draw(Item item)
        {
            currentTerminalItem = item;
            if (mainFrame == null) CreateUI(item);
            mainFrame.AddToGUIUpdateList();
            var containers = item.GetComponents<ItemContainer>().ToList();
            if (containers.Count >= 2)
            {
                containers[0].Inventory.RectTransform = leftHolder.RectTransform;
                containers[1].Inventory.RectTransform = rightHolder.RectTransform;
                var cam = GameMain.GameScreen.Cam;
                if (cam != null)
                {
                    containers[0].Inventory.Update((float)Timing.Step, cam);
                    containers[1].Inventory.Update((float)Timing.Step, cam);
                }
            }
            item.IsHighlighted = true;
            if (Character.Controlled != null) Character.Controlled.SelectedItem = item;
        }
        public void RequestRefresh() 
        { 
            mainFrame = null; 
        }
        private void CreateUI(Item item)
        {
            mainFrame = new GUIFrame(new RectTransform(new Vector2(0.6f, 0.78f), GUI.Canvas, Anchor.Center) { RelativeOffset = new Vector2(0f, -0.05f) }, style: "ItemUI");
            var mainLayout = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.95f), mainFrame.RectTransform, Anchor.Center));
            var topBar = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.12f), mainLayout.RectTransform), isHorizontal: true);
            new GUITextBlock(new RectTransform(new Vector2(0.4f, 1f), topBar.RectTransform), "数字化仓库系统", font: GUIStyle.SubHeadingFont) { TextColor = Color.LightGreen };
            var searchBox = new GUITextBox(new RectTransform(new Vector2(0.55f, 0.8f), topBar.RectTransform, Anchor.CenterRight), text: currentSearch, createClearButton: true);
            searchBox.OnTextChanged += (box, text) => { currentSearch = text; RefreshList(); return true; };
            var centerLayout = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.62f), mainLayout.RectTransform), isHorizontal: true) { AbsoluteSpacing = 10 };
            var categoryLayout = new GUILayoutGroup(new RectTransform(new Vector2(0.15f, 1f), centerLayout.RectTransform)) { AbsoluteSpacing = 3 };
            string[] cats = { "全部", "材料", "医疗", "武器", "电器", "工具", "杂项" };
            foreach (var c in cats)
            {
                new GUIButton(new RectTransform(new Vector2(1f, 0.12f), categoryLayout.RectTransform), c, style: "GUIButtonSmall") { OnClicked = (b, obj) => { selectedCategory = c; RefreshList(); return true; } };
            }
            itemList = new GUIListBox(new RectTransform(new Vector2(0.85f, 1f), centerLayout.RectTransform)) { Spacing = 2 };
            RefreshList();
            var bottomArea = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.26f), mainLayout.RectTransform), isHorizontal: true) { AbsoluteSpacing = 30 };
            var leftGroup = new GUILayoutGroup(new RectTransform(new Vector2(0.48f, 1f), bottomArea.RectTransform), false, Anchor.Center);
            new GUITextBlock(new RectTransform(new Vector2(1f, 0.35f), leftGroup.RectTransform), "存储入口", font: GUIStyle.SubHeadingFont, textAlignment: Alignment.Center) { TextColor = Color.LightCyan };
            leftHolder = new GUIFrame(new RectTransform(new Point(90, 90), leftGroup.RectTransform, Anchor.Center), style: "InnerFrameDark") { CanBeFocused = false };
            var rightGroup = new GUILayoutGroup(new RectTransform(new Vector2(0.48f, 1f), bottomArea.RectTransform), false, Anchor.Center);
            new GUITextBlock(new RectTransform(new Vector2(1f, 0.35f), rightGroup.RectTransform), "提取出口", font: GUIStyle.SubHeadingFont, textAlignment: Alignment.Center) { TextColor = Color.LightCyan };
            rightHolder = new GUIFrame(new RectTransform(new Point(90, 90), rightGroup.RectTransform, Anchor.Center), style: "InnerFrameDark") { CanBeFocused = false };
            var containers = item.GetComponents<ItemContainer>().ToList();
            if (containers.Count >= 2)
            {
                new GUICustomComponent(new RectTransform(Vector2.One, leftHolder.RectTransform), (sb, comp) => { containers[0].Inventory.Draw(sb, false); }, null);
                new GUICustomComponent(new RectTransform(Vector2.One, rightHolder.RectTransform), (sb, comp) => { containers[1].Inventory.Draw(sb, false); }, null);
            }
        }
        private void RefreshList()
        {
            if (itemList == null) return;
            itemList.Content.ClearChildren();
            var validItems = StoredItems.Where(kvp => kvp.Value.Count > 0)
                .OrderBy(kvp => ItemPrefab.Prefabs.FirstOrDefault(p => p.Identifier.Value == kvp.Key)?.Name.Value ?? "");
            foreach (var kvp in validItems)
            {
                var prefab = ItemPrefab.Prefabs.FirstOrDefault(p => p.Identifier.Value == kvp.Key);
                if (prefab == null) continue;
                if (!string.IsNullOrEmpty(currentSearch) && !prefab.Name.Value.ToLower().Contains(currentSearch.ToLower())) continue;
                if (selectedCategory != "全部" && !MatchCategory(prefab, selectedCategory)) continue;
                var element = new GUIFrame(new RectTransform(new Vector2(1f, 0.18f), itemList.Content.RectTransform), style: "ListBoxElement");
                var layout = new GUILayoutGroup(new RectTransform(Vector2.One, element.RectTransform), isHorizontal: true) { Stretch = true };
                new GUIImage(new RectTransform(new Vector2(0.12f, 1f), layout.RectTransform), prefab.InventoryIcon ?? prefab.Sprite, scaleToFit: true);
                var info = new GUILayoutGroup(new RectTransform(new Vector2(0.42f, 1f), layout.RectTransform));
                new GUITextBlock(new RectTransform(new Vector2(1f, 0.6f), info.RectTransform), prefab.Name, font: GUIStyle.SmallFont);
                new GUITextBlock(new RectTransform(new Vector2(1f, 0.4f), info.RectTransform), $"库存: {kvp.Value.Count}", font: GUIStyle.SmallFont) { TextColor = Color.LightCyan };
                var btns = new GUILayoutGroup(new RectTransform(new Vector2(0.43f, 1f), layout.RectTransform), isHorizontal: true) { AbsoluteSpacing = 2 };
                new GUIButton(new RectTransform(new Vector2(0.33f, 0.8f), btns.RectTransform), "x1", style: "GUIButtonSmall") { OnClicked = (b, o) => { SendWithdrawRequest(prefab.Identifier.Value, 1); return true; } };
                new GUIButton(new RectTransform(new Vector2(0.33f, 0.8f), btns.RectTransform), "一组", style: "GUIButtonSmall") { OnClicked = (b, o) => { SendWithdrawRequest(prefab.Identifier.Value, prefab.MaxStackSize); return true; } };
                new GUIButton(new RectTransform(new Vector2(0.33f, 0.8f), btns.RectTransform), "全部", style: "GUIButtonSmall") { OnClicked = (b, o) => { SendWithdrawRequest(prefab.Identifier.Value, kvp.Value.Count); return true; } };
            }
        }
        private bool MatchCategory(ItemPrefab p, string cat)
        {
            return cat switch { "材料" => p.Category == MapEntityCategory.Material, "医疗" => p.Category == MapEntityCategory.Medical, "武器" => p.Category == MapEntityCategory.Weapon, "电器" => p.Category == MapEntityCategory.Electrical, "工具" => p.Category == MapEntityCategory.Equipment, "杂项" => p.Category == MapEntityCategory.Misc, _ => false };
        }
        private void SendWithdrawRequest(string id, int count)
        {
            if (currentTerminalItem == null || GameMain.LuaCs?.Networking == null) return;
            InternalWithdraw(id, count);
        }
        public void InternalWithdraw(string id, int count)
        {
            if (!StoredItems.ContainsKey(id)) return;
            var list = StoredItems[id];
            int toWithdraw = Math.Min(count, list.Count);
            var containers = currentTerminalItem?.GetComponents<ItemContainer>().ToList();
            if (containers == null || containers.Count < 2) return;
            for (int i = 0; i < toWithdraw; i++)
            {
                if (list.Count == 0) break;
                var data = list.Last();
                list.RemoveAt(list.Count - 1);
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.Prefabs.First(p => p.Identifier.Value == id), containers[1].Inventory, data.Condition, data.Quality, (Item spawned) => { SpawnContained(spawned, data.ContainedItems); });
            }
            SaveData();
            RequestRefresh();
        }
        private void SpawnContained(Item parent, List<SuperTerminal.DigitalItemData> containedData)
        {
            var container = parent.GetComponent<ItemContainer>();
            if (container == null || containedData.Count == 0) return;
            foreach (var d in containedData)
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.Prefabs.First(p => d.PrefabIdentifier != null && p.Identifier.Value == d.PrefabIdentifier), container.Inventory, d.Condition, d.Quality, (Item innerItem) => { SpawnContained(innerItem, d.ContainedItems); });
        }
    }
}