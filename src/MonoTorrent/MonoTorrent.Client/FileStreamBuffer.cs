//
// FileStreamBuffer.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Collections.Generic;
using System.IO;
using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    class FileStreamBuffer : IDisposable
    {
        internal readonly struct RentedStream : IDisposable
        {
            internal readonly TorrentFileStream Stream;

            public RentedStream (TorrentFileStream stream)
            {
                Stream = stream;
                stream?.Use ();
            }

            public void Dispose ()
            {
                Stream?.Release ();
            }

        }
        // A list of currently open filestreams. Note: The least recently used is at position 0
        // The most recently used is at the last position in the array
        readonly int MaxStreams;

        public int Count => Streams.Count;

        Dictionary<TorrentFile, TorrentFileStream> Streams { get; }
        List<TorrentFile> UsageOrder { get; }

        public FileStreamBuffer (int maxStreams)
        {
            MaxStreams = maxStreams;
            Streams = new Dictionary<TorrentFile, TorrentFileStream> (maxStreams);
            UsageOrder = new List<TorrentFile> ();
        }

        internal async ReusableTask<bool> CloseStreamAsync (TorrentFile file)
        {
            using var rented = GetStream (file);
            if (rented.Stream != null) {
                await rented.Stream.FlushAsync ();
                CloseAndRemove (file, rented.Stream);
                return true;
            }

            return false;
        }

        internal async ReusableTask FlushAsync (TorrentFile file)
        {
            using var rented = GetStream (file);
            await rented.Stream?.FlushAsync ();
        }

        internal RentedStream GetStream (TorrentFile file)
        {
            if (Streams.TryGetValue (file, out TorrentFileStream stream))
                return new RentedStream (stream);
            return new RentedStream (null);
        }

        internal RentedStream GetStream (TorrentFile file, FileAccess access)
        {
            TorrentFileStream s;
            if (!Streams.TryGetValue (file, out s))
                s = null;

            if (s != null) {
                // If we are requesting write access and the current stream does not have it
                if (((access & FileAccess.Write) == FileAccess.Write) && !s.CanWrite) {
                    Logger.Log (null, "Didn't have write permission - reopening");
                    CloseAndRemove (file, s);
                    s = null;
                } else {
                    // Place the filestream at the end so we know it's been recently used
                    Streams.Remove (file);
                    Streams.Add (file, s);
                }
            }

            if (s == null) {
                if (!File.Exists (file.FullPath)) {
                    if (!string.IsNullOrEmpty (Path.GetDirectoryName (file.FullPath)))
                        Directory.CreateDirectory (Path.GetDirectoryName (file.FullPath));
                    NtfsSparseFile.CreateSparse (file.FullPath, file.Length);
                }
                s = new TorrentFileStream (file.FullPath, FileMode.OpenOrCreate, access, FileShare.ReadWrite);

                // Ensure that we truncate existing files which are too large
                if (s.Length > file.Length) {
                    if (!s.CanWrite) {
                        s.Close ();
                        s = new TorrentFileStream (file.FullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                    }
                    s.SetLength (file.Length);
                }

                Add (file, s);
            }

            return new RentedStream (s);
        }

        void Add (TorrentFile file, TorrentFileStream stream)
        {
            Logger.Log (null, "Opening filestream: {0}", file.FullPath);

            if (MaxStreams != 0 && Streams.Count >= MaxStreams) {
                for (int i = 0; i < UsageOrder.Count; i++) {
                    if (!Streams[UsageOrder[i]].InUse) {
                        CloseAndRemove (UsageOrder[i], Streams[UsageOrder[i]]);
                        break;
                    }
                }
            }
            Streams.Add (file, stream);
            UsageOrder.Add (file);
        }

        void CloseAndRemove (TorrentFile file, TorrentFileStream s)
        {
            Logger.Log (null, "Closing and removing: {0}", s.Name);
            Streams.Remove (file);
            UsageOrder.Remove (file);
            s.Dispose ();
        }

        public void Dispose ()
        {
            foreach (var stream in Streams)
                stream.Value.Dispose ();

            Streams.Clear ();
            UsageOrder.Clear ();
        }
    }
}
