using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NModbusExt.DataTypes
{
    public class TypeUTF8
    {
        public static string Convert(ushort[] data)
        {
            byte[] bytes = new byte[data.Length * 2];

            int i = 0;
            foreach (var x in data)
            {
                bytes[i] = (byte)((x & 0xff00) >> 8);
                i++;
                bytes[i] = (byte)(x & 0xff);
                i++;
            }

            return System.Text.Encoding.UTF8.GetString(bytes);

            //StringBuilder sb = new StringBuilder();
            //foreach (var x in data)
            //{
            //    sb.Append((char)((x & 0x7f00) >> 8));
            //    sb.Append((char)(x & 0x7f));
            //}
            //return sb.ToString();
        }
    }
}
