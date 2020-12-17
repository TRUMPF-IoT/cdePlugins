using System;

namespace NModbusExt.DataTypes
{
    public class TypeDouble
    {

        public enum ByteOrder { ABCD, CDAB, BADC, DCBA };

        public static double Convert(ushort[] data, ByteOrder order)
        {
            UInt64 n = 0;
            switch (order)
            {
                case ByteOrder.ABCD:
                    n = concat(data[0], data[1], data[2], data[3]);
                    break;
                case ByteOrder.DCBA:
                    n = concat(data[3], data[2], data[1], data[0]);
                    break;
                case ByteOrder.CDAB:
                    n = concat(data[2], data[3], data[0], data[1]);
                    break;
                case ByteOrder.BADC:
                    n = concat(data[1], data[0], data[3], data[2]);
                    break;
            }
            return toDouble(n);
        }

        static UInt64 concat(ushort n1, ushort n2, ushort n3, ushort n4)
        {
            return ((ulong)n1 << 48) | ((ulong)n2 << 32) | ((ulong)n3 << 16) | (ulong)n4;
        }


        static unsafe double toDouble(UInt64 value)
        {
            return *((double*)&value);
        }


    }
}
