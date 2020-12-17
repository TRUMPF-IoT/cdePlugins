using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NModbusExt.DataTypes
{
    public class TypeUInt16
    {
        public static int Convert(ushort[] data)
        {
            return data[0];
        }
    }
}
