//
// DiskWriter.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.IO;
using System.Threading;
using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    public class DiskWriter : IPieceWriter
    {
        readonly SemaphoreSlim Limiter;

        public int OpenFiles => StreamCache.Count;

        readonly FileStreamBuffer StreamCache;

        public DiskWriter ()
            : this (196)
        {

        }

        public DiskWriter (int maxOpenFiles)
        {
            StreamCache = new FileStreamBuffer (maxOpenFiles);
            Limiter = new SemaphoreSlim (maxOpenFiles);
        }

        public void Dispose ()
        {
            StreamCache.Dispose ();
        }

        public async ReusableTask CloseAsync (TorrentFile file)
        {
            await StreamCache.CloseStreamAsync (file);
        }

        public ReusableTask<bool> ExistsAsync (TorrentFile file)
        {
            return ReusableTask.FromResult (File.Exists (file.FullPath));
        }

        public async ReusableTask FlushAsync (TorrentFile file)
            => await StreamCache.FlushAsync (file);

        public async ReusableTask MoveAsync (TorrentFile file, string newPath, bool overwrite)
        {
            await StreamCache.CloseStreamAsync (file);

            if (overwrite)
                File.Delete (newPath);
            File.Move (file.FullPath, newPath);
        }

        public async ReusableTask<int> ReadAsync (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File (file);
            Check.Buffer (buffer);

            if (offset < 0 || offset + count > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                using var rented = StreamCache.GetStream (file, FileAccess.Read);
                if (rented.Stream.Length < offset + count)
                    return 0;

                if (rented.Stream.Position != offset)
                    rented.Stream.Seek (offset, SeekOrigin.Begin);
                return await rented.Stream.ReadAsync (buffer, bufferOffset, count);
            }
        }

        public async ReusableTask WriteAsync (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File (file);
            Check.Buffer (buffer);

            if (offset < 0 || offset + count > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                using var rented = StreamCache.GetStream (file, FileAccess.ReadWrite);
                rented.Stream.Seek (offset, SeekOrigin.Begin);
                await rented.Stream.WriteAsync (buffer, bufferOffset, count);
            }
        }
    }
}
