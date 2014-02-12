using System;
using System.IO;
using System.Threading.Tasks;

namespace TplTests
{
    internal class StreamWrapper : Stream
    {
        private readonly Stream _innerStream;
        private readonly string _path;

        public StreamWrapper(Stream innerStream, string directory, string fileName)
        {
            _innerStream = innerStream;
            _path = Path.Combine(directory, fileName);
        }

        public override void Flush()
        {
            Log("Flush");
            _innerStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Log("Seek - offset: {0}; origin: {1}", offset, origin);
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Log("SetLength - value: {0}", value);
            _innerStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Log("Read - offset: {0}; count: {1}", offset, count);
            var result = _innerStream.Read(buffer, offset, count);
            Log("Read - result: {0}", result);
            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Log("Write - offset: {0}; count: {1}", offset, count);
            _innerStream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                Log("CanRead");
                var result = _innerStream.CanRead;
                Log("CanRead - result: {0}", result);
                return result;
            }
        }

        public override bool CanSeek
        {
            get
            {
                Log("CanSeek");
                var result = _innerStream.CanSeek;
                Log("CanSeek - result: {0}", result);
                return result;
            }
        }

        public override bool CanWrite
        {
            get
            {
                Log("CanWrite");
                var result = _innerStream.CanWrite;
                Log("CanWrite - result: {0}", result);
                return result;
            }
        }

        public override long Length
        {
            get
            {
                Log("Length");
                var result = _innerStream.Length;
                Log("Length - result: {0}", result);
                return result;
            }
        }

        public override long Position {
            get
            {
                Log("Position");
                var result = _innerStream.Position;
                Log("Position - result: {0}", result);
                return result;
            }
            set
            {
                Log("Position - value: {0}", value);
                _innerStream.Position = value;
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Log("BeginRead - offset: {0}; count: {1}", offset, count);
            return _innerStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Log("BeginWrite - offset: {0}; count: {1}", offset, count);
            return _innerStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            Log("EndRead");
            var result = _innerStream.EndRead(asyncResult);
            Log("EndRead - result: {0}", result);
            return result;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            Log("EndWrite");
            _innerStream.EndWrite(asyncResult);
        }

        public override Task FlushAsync(System.Threading.CancellationToken cancellationToken)
        {
            Log("FlushAsync");
            return _innerStream.FlushAsync(cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            Log("ReadAsync - offset: {0}, count: {1}", offset, count);
            return _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            Log("WriteAsync - offset: {0}, count: {1}", offset, count);
            return _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            Log("ReadByte");
            return _innerStream.ReadByte();
        }

        public override void WriteByte(byte value)
        {
            Log("WriteByte");
            _innerStream.WriteByte(value);
        }

        public override void Close()
        {
            Log("Close");
            _innerStream.Close();
        }

        public override bool CanTimeout
        {
            get
            {
                Log("CanTimeout");
                var result = _innerStream.CanTimeout;
                Log("CanTimeout - result: {0}", result);
                return result;
            }
        }

        public override int ReadTimeout
        {
            get
            {
                Log("ReadTimeout");
                var result = _innerStream.ReadTimeout;
                Log("ReadTimeout - result: {0}", result);
                return result;
            }
            set
            {
                Log("ReadTimeout - value: {0}", value);
                _innerStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                Log("WriteTimeout");
                var result = _innerStream.WriteTimeout;
                Log("WriteTimeout - result: {0}", result);
                return result;
            }
            set
            {
                Log("WriteTimeout - value: {0}", value);
                _innerStream.WriteTimeout = value;
            }
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, System.Threading.CancellationToken cancellationToken)
        {
            Log("CopyToAsync - bufferSize: {0}", bufferSize);
            return _innerStream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        private void Log(string format, params object[] args)
        {
            var message = string.Format(format, args);
            message += string.Format(" ({0})", _path);
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}
