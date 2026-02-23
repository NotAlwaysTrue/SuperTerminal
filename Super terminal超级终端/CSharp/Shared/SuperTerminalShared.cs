using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Xml.Linq;
namespace SuperTerminal
{
    public class SuperTerminalPlugin : IAssemblyPlugin
    {
        private Harmony harmony;
        public static Dictionary<string, List<DigitalItemData>> StoredItems = new();
        public static bool IsWithdrawing = false;
        private static string GetSavePath()
        {
            string saveIdentifier = "global";
            if (GameMain.GameSession?.DataPath.SavePath != null)
                saveIdentifier = Path.GetFileNameWithoutExtension(GameMain.GameSession.DataPath.SavePath);
            return Path.Combine(SaveUtil.DefaultSaveFolder, $"super_terminal_{saveIdentifier}.xml");
        }
        public void Initialize()
        {
            harmony = new Harmony("com.superterminal.storage");
            harmony.PatchAll();
            if (GameMain.LuaCs?.Networking != null)
            {
                GameMain.LuaCs.Networking.RequestId("ST_Sync");
                GameMain.LuaCs.Networking.RequestId("ST_ReqWD");
                GameMain.LuaCs.Networking.Receive("ST_Sync", (object[] args) => {
                    if (args.Length > 0 && args[0] is IReadMessage msg)
                        SuperTerminalPlugin.DeserializeFromNetString(msg.ReadString());
                });
                GameMain.LuaCs.Networking.Receive("ST_ReqWD", (object[] args) => {
                    if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsServer)
                    {
                        if (args.Length > 0 && args[0] is IReadMessage msg)
                        {
                            string id = msg.ReadString();
                            int count = msg.ReadInt32();
                            SuperTerminalUI.InternalWithdraw(id, count);
                        }
                    }
                });
            }
            LuaCsLogger.Log("[SuperTerminal] V20.0: Multiplayer Delay-Sync Architecture.");
        }
        public void OnLoadCompleted() { LoadData(); if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsServer) SyncToClients(); }
        public void Dispose() { harmony?.UnpatchSelf(); }
        public void PreInitPatching() { }
        public static void SaveData()
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) return;
            try
            {
                XElement root = new XElement("StoredItems");
                foreach (var kvp in StoredItems)
                {
                    XElement itemGroup = new XElement("ItemGroup", new XAttribute("id", kvp.Key));
                    foreach (var data in kvp.Value)
                    {
                        XElement itemData = new XElement("Item", new XAttribute("condition", data.Condition), new XAttribute("quality", data.Quality));
                        if (data.ContainedItems.Count > 0) SaveContained(itemData, data.ContainedItems);
                        itemGroup.Add(itemData);
                    }
                    root.Add(itemGroup);
                }
                root.Save(GetSavePath());
            }
            catch { }
        }
        private static void SaveContained(XElement parent, List<DigitalItemData> contained)
        {
            foreach (var d in contained)
            {
                XElement c = new XElement("Contained", new XAttribute("id", d.PrefabIdentifier), new XAttribute("condition", d.Condition), new XAttribute("quality", d.Quality));
                if (d.ContainedItems.Count > 0) SaveContained(c, d.ContainedItems);
                parent.Add(c);
            }
        }
        public static void LoadData()
        {
            string path = GetSavePath();
            StoredItems.Clear();
            if (!File.Exists(path)) return;
            try
            {
                XElement root = XElement.Load(path);
                foreach (XElement group in root.Elements("ItemGroup"))
                {
                    string id = group.Attribute("id").Value;
                    var list = new List<DigitalItemData>();
                    foreach (XElement item in group.Elements("Item"))
                    {
                        var d = new DigitalItemData { PrefabIdentifier = id, Condition = float.Parse(item.Attribute("condition").Value), Quality = int.Parse(item.Attribute("quality").Value) };
                        foreach (XElement c in item.Elements("Contained")) d.ContainedItems.Add(new DigitalItemData { PrefabIdentifier = c.Attribute("id").Value, Condition = float.Parse(c.Attribute("condition").Value), Quality = int.Parse(c.Attribute("quality").Value) });
                        list.Add(d);
                    }
                    StoredItems[id] = list;
                }
            }
            catch { }
        }
        public static void SyncToClients()
        {
            if (GameMain.NetworkMember == null || !GameMain.NetworkMember.IsServer || GameMain.LuaCs?.Networking == null) return;
            var msg = GameMain.LuaCs.Networking.Start("ST_Sync");
            if (msg == null) return;
            msg.WriteString(SerializeToNetString());
            GameMain.LuaCs.Networking.Send(msg, DeliveryMethod.Reliable);
        }
        private static string SerializeToNetString()
        {
            XElement root = new XElement("S");
            foreach (var kvp in StoredItems)
            {
                XElement g = new XElement("G", new XAttribute("i", kvp.Key));
                foreach (var d in kvp.Value) g.Add(new XElement("I", new XAttribute("c", d.Condition), new XAttribute("q", d.Quality)));
                root.Add(g);
            }
            return root.ToString();
        }
        public static void DeserializeFromNetString(string data)
        {
            try
            {
                XElement root = XElement.Parse(data);
                StoredItems.Clear();
                foreach (XElement group in root.Elements("G"))
                {
                    string id = group.Attribute("i").Value;
                    var list = group.Elements("I").Select(i => new DigitalItemData { PrefabIdentifier = id, Condition = float.Parse(i.Attribute("c").Value), Quality = int.Parse(i.Attribute("q").Value) }).ToList();
                    StoredItems[id] = list;
                }
                SuperTerminalUI.RequestRefresh();
            }
            catch { }
        }
    }
    public class DigitalItemData { public string PrefabIdentifier; public float Condition; public int Quality; public List<DigitalItemData> ContainedItems = new(); }
    public static class DeferredActionQueue
    {
        private static List<Action> actions = new List<Action>();
        private static float delay = 0f;
        public static void Enqueue(Action action) { actions.Add(action); }
        public static void Update(float deltaTime)
        {
            if (actions.Count == 0) return;
            delay += deltaTime;
            if (delay < 0.5f) return;
            foreach (var action in actions) { action(); }
            actions.Clear();
            delay = 0f;

            SuperTerminalPlugin.SaveData();
            SuperTerminalPlugin.SyncToClients();
            SuperTerminalUI.RequestRefresh();
        }
    }
    [HarmonyPatch(typeof(GameMain), "Update")]
    public static class CoreUpdatePatch
    {
        public static void Postfix(GameTime gameTime)
        {
            if (GameMain.NetworkMember != null && !GameMain.NetworkMember.IsServer) return;
            DeferredActionQueue.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        }
    }
    [HarmonyPatch(typeof(ItemContainer), "OnItemContained")]
    public static class ContainerPatch
    {
        public static void Postfix(ItemContainer __instance, Item containedItem)
        {
            if (containedItem == null || SuperTerminalPlugin.IsWithdrawing) return;
            if (GameMain.NetworkMember != null && !GameMain.NetworkMember.IsServer) return;
            bool isSuperTerminalInput = (__instance.Item.Prefab.Identifier == "super_terminal" && __instance.Item.GetComponents<ItemContainer>().ToList().IndexOf(__instance) == 0);
            bool isStorageEntrance = (__instance.Item.Prefab.Identifier == "storage_entrance");
            if (isSuperTerminalInput || isStorageEntrance)
            {
                DeferredActionQueue.Enqueue(() => {
                    if (containedItem.Removed || containedItem.ParentInventory != __instance.Inventory) return;
                    var data = Digitize(containedItem);

                    if (!SuperTerminalPlugin.StoredItems.ContainsKey(containedItem.Prefab.Identifier.Value))
                        SuperTerminalPlugin.StoredItems[containedItem.Prefab.Identifier.Value] = new List<DigitalItemData>();
                    SuperTerminalPlugin.StoredItems[containedItem.Prefab.Identifier.Value].Add(data);
                    containedItem.Drop(null, createNetworkEvent: true);
                    __instance.Inventory.RemoveItem(containedItem);
                    Entity.Spawner?.AddEntityToRemoveQueue(containedItem);

                    Barotrauma.DebugConsole.Log($"[SuperTerminal] Safely digested {containedItem.Name}");
                });
            }
        }
        private static DigitalItemData Digitize(Item item)
        {
            var data = new DigitalItemData { PrefabIdentifier = item.Prefab.Identifier.Value, Condition = item.Condition, Quality = item.Quality };
            var container = item.GetComponent<ItemContainer>();
            if (container != null)
            {
                foreach (var innerItem in container.Inventory.AllItems.ToList())
                {
                    data.ContainedItems.Add(Digitize(innerItem));
                    innerItem.Drop(null);
                    Entity.Spawner?.AddEntityToRemoveQueue(innerItem);
                }
            }
            return data;
        }
    }
    [HarmonyPatch(typeof(EntitySpawner), "Update")]
    public static class SpawnerLockFix { public static void Postfix() => SuperTerminalPlugin.IsWithdrawing = false; }
    [HarmonyPatch(typeof(ItemComponent), "AddToGUIUpdateList")]
    public static class SilentContainerUIPatch { public static bool Prefix(ItemComponent __instance) => !(__instance is ItemContainer container && container.Item.Prefab.Identifier == "super_terminal"); }
    [HarmonyPatch(typeof(Item), "UpdateHUD")]
    public static class SafeHUDInjection
    {
        public static void Postfix(Item __instance, Character character)
        {
            if (__instance.Prefab.Identifier != "super_terminal" || character != Character.Controlled) return;
            foreach (var c in __instance.GetComponents<ItemContainer>()) if (!__instance.activeHUDs.Contains(c)) __instance.activeHUDs.Add(c);
        }
    }
    [HarmonyPatch(typeof(Terminal), "AddToGUIUpdateList")]
    public static class UIHijackPatch { public static bool Prefix(Terminal __instance) { if (__instance.Item.Prefab.Identifier == "super_terminal") { SuperTerminalUI.Draw(__instance.Item); return false; } return true; } }
    public static class SuperTerminalUI
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
        public static void RequestRefresh() { mainFrame = null; }
        private static void CreateUI(Item item)
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
        private static void RefreshList()
        {
            if (itemList == null) return;
            itemList.Content.ClearChildren();
            var validItems = SuperTerminalPlugin.StoredItems.Where(kvp => kvp.Value.Count > 0)
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
        private static bool MatchCategory(ItemPrefab p, string cat)
        {
            return cat switch { "材料" => p.Category == MapEntityCategory.Material, "医疗" => p.Category == MapEntityCategory.Medical, "武器" => p.Category == MapEntityCategory.Weapon, "电器" => p.Category == MapEntityCategory.Electrical, "工具" => p.Category == MapEntityCategory.Equipment, "杂项" => p.Category == MapEntityCategory.Misc, _ => false };
        }
        private static void SendWithdrawRequest(string id, int count)
        {
            if (currentTerminalItem == null || GameMain.LuaCs?.Networking == null) return;
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient)
            {
                var msg = GameMain.LuaCs.Networking.Start("ST_ReqWD");
                if (msg != null)
                {
                    msg.WriteString(id);
                    msg.WriteInt32(count);
                    GameMain.LuaCs.Networking.Send(msg, DeliveryMethod.Reliable);
                }
            }
            else
            {
                InternalWithdraw(id, count);
            }
        }
        public static void InternalWithdraw(string id, int count)
        {
            if (!SuperTerminalPlugin.StoredItems.ContainsKey(id)) return;
            var list = SuperTerminalPlugin.StoredItems[id];
            int toWithdraw = Math.Min(count, list.Count);
            var containers = currentTerminalItem?.GetComponents<ItemContainer>().ToList();
            if (containers == null || containers.Count < 2) return;
            SuperTerminalPlugin.IsWithdrawing = true;
            for (int i = 0; i < toWithdraw; i++)
            {
                if (list.Count == 0) break;
                var data = list.Last();
                list.RemoveAt(list.Count - 1);
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.Prefabs.First(p => p.Identifier.Value == id), containers[1].Inventory, data.Condition, data.Quality, (Item spawned) => { SpawnContained(spawned, data.ContainedItems); });
            }
            SuperTerminalPlugin.SaveData();
            SuperTerminalPlugin.SyncToClients();
            RequestRefresh();
        }
        private static void SpawnContained(Item parent, List<DigitalItemData> containedData)
        {
            var container = parent.GetComponent<ItemContainer>();
            if (container == null || containedData.Count == 0) return;
            foreach (var d in containedData)
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.Prefabs.First(p => d.PrefabIdentifier != null && p.Identifier.Value == d.PrefabIdentifier), container.Inventory, d.Condition, d.Quality, (Item innerItem) => { SpawnContained(innerItem, d.ContainedItems); });
        }
    }
}