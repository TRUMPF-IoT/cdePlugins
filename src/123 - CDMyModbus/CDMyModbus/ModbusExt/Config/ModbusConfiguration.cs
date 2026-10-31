using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace NModbusExt.Config
{
    public class ModbusConfiguration
    {
        public int PollingInterval { get; set; }
        public List<TheDeviceTagMapping> DeviceTypes { get; set; }
        public List<TheDeviceDescription> Devices { get; set; }

        public ModbusConfiguration()
        {
            DeviceTypes = new List<TheDeviceTagMapping>();
            Devices = new List<TheDeviceDescription>();
        }

        public static ModbusConfiguration ReadFromFile(string path)
        {
            var xml = new XmlDocument();
            xml.Load(path);

            var config = new ModbusConfiguration();

            foreach (XmlNode node in xml.ChildNodes)
            {
                //Console.WriteLine("Node: " + node.Name);

                if (node.Name == "modbus")
                {
                    ReadModbus(config, node);
                }
            }

            return config;
        }

        static void ReadModbus(ModbusConfiguration config, XmlNode node)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == "pollinginterval")
                {
                    int value = 0;
                    bool r = int.TryParse(child.InnerText, out value);
                    if (!r)
                    {
                        value = 10;
                    }
                    config.PollingInterval = value;
                }
                else if (child.Name == "mapping")
                {
                    var dm = new TheDeviceTagMapping();
                    ReadDeviceTypeMapping(dm, child);
                    config.DeviceTypes.Add(dm);
                }
                else if (child.Name == "device")
                {
                    var dd = new TheDeviceDescription();
                    ReadDeviceDescription(config, dd, child);
                    config.Devices.Add(dd);
                }
            }
        }

        static void ReadDeviceTypeMapping(TheDeviceTagMapping dm, XmlNode node)
        {
            dm.Name = node.Attributes["name"].Value;
          
            if (node.Attributes["offset"] != null)
            {
                //dm.Offset = ParseInt(node.Attributes["offset"].Value, 0); //CM: Why would there be a another offset if there is an offset in the fields?
            }

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == "field")
                {
                    var fm = new Dictionary<string, object>();
                    ReadFieldMapping(ref fm, child);
                    dm.FieldList.Add(fm);
                }
            }
        }

        static void ReadFieldMapping(ref Dictionary<string, object> fm, XmlNode node)
        {
            fm["FieldName"] = node.Attributes["name"].Value;
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == "source")
                {
                    fm["SourceOffset"] = ParseInt(child.Attributes["offset"].Value, 0);
                    fm["SourceSize"] = ParseInt(child.Attributes["size"].Value, 0);
                    fm["SourceType"] = child.Attributes["type"].Value;
                }
                else if (child.Name == "property")
                {
                    fm["PropertyName"] = child.Attributes["name"].Value;
                }
            }
        }

        static void ReadDeviceDescription(ModbusConfiguration config, TheDeviceDescription dd, XmlNode node)
        {
            dd.Properties["FriendlyName"] = node.Attributes["name"].Value;
            dd.Properties["ID"] = node.Attributes["id"].Value;
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == "network")
                {
                    dd.Properties["Address"] = child.Attributes["ipaddr"].Value;
                    if (child.Attributes["port"] != null)
                    {
                    dd.Properties["CustomPort"] = ParseInt(child.Attributes["port"].Value, 502);
                }
                }
                else if (child.Name == "mapping")
                {
                    dd.TagMappings[child.Attributes["name"].Value] = config.DeviceTypes.Single(x => x.Name == child.Attributes["name"].Value);
                }
                else if (child.Name == "slave")
                {
                    dd.Properties["SlaveAddress"] = ParseInt(child.Attributes["address"].Value, 126);
                }
            }
        }

        static int ParseInt(string str, int defaultValue)
        {
            int v = 0;
            bool r = int.TryParse(str, out v);
            if (r)
            {
                return v;
            }
            else
            {
                return defaultValue;
            }
        }

    }

}
