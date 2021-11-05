namespace Modbus.Serial
{
    using System;
    using System.Diagnostics;
    using System.IO.Ports;
    using System.Text;
    using System.Threading;
    using global::Modbus.IO;

    public interface IGPIOController
    {
        void Write(int pin, bool IsOuput);
        byte Read(int pin);
    }

    /// <summary>
    ///     Concrete Implementor - http://en.wikipedia.org/wiki/Bridge_Pattern
    /// </summary>
    public class SerialPortAdapter : IStreamResource
    {
        private const string NewLine = "\r\n";
        private SerialPort _serialPort;
        private Action<bool, byte[]> b4ReadWrite;
        private Action<bool, byte[], int> AfterReadWrite;

        public SerialPortAdapter(SerialPort serialPort, Action<bool, byte[]> pb4ReadWrite=null, Action<bool, byte[], int> pAfterReadWrite =null)
        {
            Debug.Assert(serialPort != null, "Argument serialPort cannot be null.");

            _serialPort = serialPort;
            _serialPort.NewLine = NewLine;

            b4ReadWrite = pb4ReadWrite;
            AfterReadWrite = pAfterReadWrite;
        }

        public int InfiniteTimeout
        {
            get { return SerialPort.InfiniteTimeout; }
        }

        public int ReadTimeout
        {
            get { return _serialPort.ReadTimeout; }
            set { _serialPort.ReadTimeout = value; }
        }

        public int WriteTimeout
        {
            get { return _serialPort.WriteTimeout; }
            set { _serialPort.WriteTimeout = value; }
        }

        public void DiscardInBuffer()
        {
            _serialPort.DiscardInBuffer();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            b4ReadWrite?.Invoke(false, buffer);
            //int read = 0;
            //while (true)
            //{
            //    if (Console.KeyAvailable)
            //    {
            //        ConsoleKeyInfo key = Console.ReadKey();
            //        if (key.Key == ConsoleKey.Escape)
            //        {
            //            read = count;
            //            break;
            //        }
            //    }
            //    if (_serialPort.BytesToRead > 0)
            //    {
            //        Console.WriteLine($"Ready to read {_serialPort.BytesToRead} bytes");
            //        read = _serialPort.Read(buffer, offset, count);
            //        Console.WriteLine($"Has Read {read} bytes: {ByteArrayToString(buffer)}");
            //        break;
            //    }
            //    Console.WriteLine("Waiting to read 1sec");
            //    Thread.Sleep(1000);
            //}
            int read=_serialPort.Read(buffer, offset, count);
            AfterReadWrite?.Invoke(false, buffer, read);
            return read;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            b4ReadWrite?.Invoke(true, buffer);
            _serialPort.Write(buffer, offset, count);
            AfterReadWrite?.Invoke(true, buffer, count);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _serialPort?.Dispose();
                _serialPort = null;
            }
        }

        #region helper
        private static string ByteArrayToString(byte[] ba)
        {
            if (ba == null)
                return null;
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}:", b);
            return hex.ToString().ToUpper();
        }
        #endregion
    }
}