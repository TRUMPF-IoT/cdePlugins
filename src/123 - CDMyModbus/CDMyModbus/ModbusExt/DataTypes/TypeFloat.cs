using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NModbusExt.DataTypes
{
    public class TypeFloat
    {

        public enum ByteOrder { ABCD, CDAB, BADC, DCBA, SinglePrecIEEE };

        static UInt32 uint32 = 0;


        public static float Convert(ushort[] data, ByteOrder order)
        {

            switch (order)
            {
                case ByteOrder.ABCD:
                    uint32 = ((uint)data[1] << 16) | (uint)data[0];
                    break;
                case ByteOrder.CDAB:
                    uint32 = ((uint)data[0] << 16) | (uint)data[1];
                    break;
                case ByteOrder.SinglePrecIEEE:
                    return ToIEEESinglePrecisionFloat(data);
            }
            return toFloat(uint32);
        }


        static unsafe float toFloat(UInt32 value)
        {
            return *((float*)&value);
        }

        static float ToIEEESinglePrecisionFloat(ushort[] buffer)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(buffer[1] & 0xFF);
            bytes[1] = (byte)(buffer[1] >> 8);
            bytes[2] = (byte)(buffer[0] & 0xFF);
            bytes[3] = (byte)(buffer[0] >> 8);
            float value = BitConverter.ToSingle(bytes, 0);
            return value;
        }

    }
}
