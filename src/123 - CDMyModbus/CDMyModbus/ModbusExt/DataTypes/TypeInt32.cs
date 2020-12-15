using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NModbusExt.DataTypes
{
    public class TypeInt32
    {
        public static Int32 Convert(ushort[] data)
        {
            return ((Int32)data[0] << 16) + (Int32)data[1];
        }
    }
}
