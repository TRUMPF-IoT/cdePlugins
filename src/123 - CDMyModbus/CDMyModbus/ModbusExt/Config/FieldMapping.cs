using nsCDEngine.ViewModels;

namespace NModbusExt.Config
{

    public class FieldMapping : TheDataBase
    {
        public int SourceOffset { get; set; }
        public int SourceSize { get; set; }
        public float ScaleFactor { get; set; }
        public string SourceType { get; set; }
        public string PropertyName { get; set; }

        public int ReadEvery { get; set; }  

        public object Value { get; set; }

        public bool AllowWrite { get; set; }
        public int ConnectionType { get; set; }
    }
}
