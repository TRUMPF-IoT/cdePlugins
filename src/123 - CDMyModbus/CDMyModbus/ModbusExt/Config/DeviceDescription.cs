using nsCDEngine.ViewModels;
using System.Collections.Generic;

namespace NModbusExt.Config
{
    public class DeviceDescription
    {
        public Dictionary<string, object> Properties { get; set; }
        public DeviceTypeMapping Mapping { get; set; }

        public DeviceDescription()
        {
        }
    }

    public class DeviceTypeMapping
    {
        public string Name { get; set; }
        public int Offset { get; set; }
        public List<FieldMapping> FieldList { get; set; }

        public DeviceTypeMapping()
        {
            FieldList = new List<FieldMapping>();
            Offset = 0;
        }
    }

    public class FieldMapping : TheDataBase
    {
        public string FieldName { get; set; }
        public int SourceOffset { get; set; }
        public int SourceSize { get; set; }
        public float ScaleFactor { get; set; }
        public string SourceType { get; set; }
        public string PropertyName { get; set; }

        public object Value { get; set; }

        public bool AllowWrite { get; set; }
        public int ConnectionType { get; set; }
    }
}
