using Barotrauma;
using Barotrauma.Items.Components;
using System.Xml.Linq;

namespace SuperTerminalMain
{
    public class SuperTerminal : ItemComponent
    {
        private static Dictionary<string, List<DigitalItemData>> StoredItems = [];

        public static string SavePath
        {
            get
            {
                string saveIdentifier = Path.GetFileNameWithoutExtension(GameMain.GameSession?.DataPath.SavePath ?? "TEMP.bruh");
                return Path.Combine(SaveUtil.DefaultSaveFolder, $"super_terminal_{saveIdentifier}.xml");
            }
        }
       
        public SuperTerminal(Item item, ContentXElement element)
            : base(item, element)
        { }
        public class DigitalItemData 
        { 
            public string PrefabIdentifier; 
            public float Condition; 
            public int Quality; 
            public List<DigitalItemData> ContainedItems = new(); 
        }

        // TODO: Refactor save/load functions to avoid recursive functions
        public void SaveData()
        {
            try
            {
                XElement root = new XElement("StoredItems");
                foreach (var kvp in StoredItems)
                {
                    XElement itemGroup = new XElement("ItemGroup", new XAttribute("id", kvp.Key));
                    foreach (var data in kvp.Value)
                    {
                        XElement itemData = new XElement("Item", new XAttribute("condition", data.Condition), new XAttribute("quality", data.Quality));
                        if (data.ContainedItems.Count > 0)
                        {
                            SaveContained(itemData, data.ContainedItems);
                        }
                        itemGroup.Add(itemData);
                    }
                    root.Add(itemGroup);
                }
                root.Save(SavePath);
            }
            catch { }
        }

        public void LoadData()
        {
            string path = SavePath;
            StoredItems.Clear();
            if (!File.Exists(path)) return;
            XElement root = XElement.Load(path);
            try {
                foreach (XElement group in root.Elements("ItemGroup"))
                {
                    string id = group.Attribute("id")?.Value;
                    var list = new List<DigitalItemData>();
                    foreach (XElement item in group.Elements("Item"))
                    {
                        var d = new DigitalItemData 
                        { 
                            PrefabIdentifier = id, 
                            Condition = float.Parse(item.Attribute("condition").Value), 
                            Quality = int.Parse(item.Attribute("quality").Value) 
                        };
                        foreach (XElement c in item.Elements("Contained"))
                        {
                            d.ContainedItems.Add(
                                new DigitalItemData 
                                { 
                                    PrefabIdentifier = c.Attribute("id").Value,
                                    Condition = float.Parse(c.Attribute("condition").Value), 
                                    Quality = int.Parse(c.Attribute("quality").Value) 
                                }
                            );
                            list.Add(d);
                        }
                    }
                        StoredItems[id] = list;
                }
            }
            catch { }
        }

        private void SaveContained(XElement parent, List<DigitalItemData> contained)
        {
            foreach (var d in contained)
            {
                XElement c = new XElement("Contained", new XAttribute("id", d.PrefabIdentifier), new XAttribute("condition", d.Condition), new XAttribute("quality", d.Quality));
                if (d.ContainedItems.Count > 0) SaveContained(c, d.ContainedItems);
                parent.Add(c);
            }
        }
    }
}