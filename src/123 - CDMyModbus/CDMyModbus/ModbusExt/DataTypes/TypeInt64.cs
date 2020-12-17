using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NModbusExt.DataTypes
{
    public class TypeInt64
    {
        public static Int64 Convert(ushort[] data)
        {
            return ((Int64)data[0] << 48) + ((Int64)data[1] << 32) + ((Int64)data[2] << 16) + (Int64)data[3];
        }
    }
}
