using System;
using System.IO;

namespace YandereSimLauncher {
    public class ProgressionStream : Stream {
        public delegate void ProgressionHandler(double progression);

        private Stream _sourceStream;
        private ProgressionHandler _progressionHandler;

        private bool isClosed;

        public ProgressionStream(Stream sourceStream, ProgressionHandler progressionHandler) {
            this._sourceStream = sourceStream;
            this._progressionHandler = progressionHandler;
        }

        public override int Read(byte[] array, int offset, int count) {
            this._progressionHandler(this.Position / (double)this.Length * 100);

            if (isClosed) _sourceStream.Dispose();

            return this._sourceStream.Read(array, offset, count);
        }

        public override void Close() {
            _sourceStream.Close();
            isClosed = true;
            base.Close();
        }

        protected override void Dispose(bool disposing) {
            _sourceStream.Dispose();
        }

        public override bool CanRead {
            get {
                return this._sourceStream.CanRead;
            }
        }

        public override bool CanSeek {
            get {
                return this._sourceStream.CanSeek;
            }
        }

        public override bool CanWrite {
            get {
                return this._sourceStream.CanWrite;
            }
        }

        public override long Length {
            get {
                return this._sourceStream.Length;
            }
        }

        public override long Position {
            get {
                return this._sourceStream.Position;
            }

            set {
                this._sourceStream.Position = value;
            }
        }

        public override void Flush() {
            this._sourceStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return this._sourceStream.Seek(offset, origin);
        }

        public override void SetLength(long value) {
            this._sourceStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            this._sourceStream.Write(buffer, offset, count);
        }
    }
}
