﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MCForge.Entity;
using MCForge.World;
using MCForge.Utils;
using System.IO;
using System.IO.Compression;
using MCForge.Utils.Settings;
using System.Threading;

namespace MCForge.Utils {
    public class BlockChangeHistory {
        static string _basepath;
        static string basepath {
            get {
                if (_basepath == null) {
                    _basepath = ServerSettings.GetSetting("BlockChangeHistoryPath");
                    if (_basepath == null) _basepath = "" + Path.DirectorySeparatorChar;
                    else _basepath = ""+Path.DirectorySeparatorChar;
                    _basepath = System.Windows.Forms.Application.StartupPath + _basepath;
                }
                if (_basepath.Length > 0 && _basepath[_basepath.Length - 1] != Path.DirectorySeparatorChar)
                    return _basepath + Path.DirectorySeparatorChar;
                else return _basepath;
            }
        }
        string GetFullPath(Level l) {
            return basepath + l.Name + Path.DirectorySeparatorChar + player.UID + Path.DirectorySeparatorChar; //TODO: make sure renaming a level moves undo histroy files to new folder.
        }

        /// <summary>
        /// File ending of blockchange history files (used by undo)
        /// </summary>
        static string historyEnding = ".bch";
        /// <summary>
        /// File ending of blockchange future files (
        /// </summary>
        static string futureEnding = ".bcf";
        public BlockChangeHistory(Player p) {
            player = p;
            
        }
        Player player;
        List<long> recentTimes = new List<long>();
        /// <summary>
        /// Item1: Position
        /// Item2: BlockOld
        /// Item3: BlockNew
        /// </summary>
        List<Tuple<Tuple<short,short,short>, byte, byte>> recentChanges = new List<Tuple<Tuple<short,short,short>, byte, byte>>();
        private readonly object lock_recent = new object();
        private static readonly object lock_archive = new object();
        #region Public Add
        public void Add(Tuple<short,short,short> pos, byte oldBlock, byte newBlock) {
            lock (lock_recent) {
                recentTimes.Add(DateTime.Now.Ticks);
                recentChanges.Add(new Tuple<Tuple<short,short,short>, byte, byte>(pos, oldBlock, newBlock));
            }
        }
        #endregion

        #region Public Undo
        public void Undo(DateTime since, Level l) { Undo(since.Ticks, l); }
        public void Undo(long since, Level l) {
            ExtraData<Tuple<short, short, short>, Tuple<long, byte>> toChange = GetOriginalBlocks(since, l);
            List<long> asked = new List<long>();
            foreach (Player p in l.Players) {
                if (p != player)
                    toChange = p.history.redoRecentOthersUndo(toChange, since, l);
            }
            toChange = redoArchiveOthersUndoForAllPlayers(toChange, since, l);
            string path=GetFullPath(l);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string tmppath = GetFullPath(l) + DateTime.Now.Ticks + futureEnding;
            FileStream fs = new FileStream(tmppath, FileMode.Create, FileAccess.Write);
            GZipStream gz = new GZipStream(fs, CompressionMode.Compress);
            BinaryWriter bw = new BinaryWriter(gz);
            foreach (Tuple<short, short, short> v in toChange.Keys) {
                //all changes are associated to the time in the filename
                byte tmp = l.GetBlock(v.Item1,v.Item2,v.Item3);
                if (tmp != toChange[v].Item2) {
                    bw.Write(v.Item1);//coords
                    bw.Write(v.Item2);
                    bw.Write(v.Item3);
                    bw.Write(toChange[v].Item2);//after undo
                    l.BlockChange(new Vector3S(v.Item1,v.Item2,v.Item3), toChange[v].Item2);
                }
            }
            bw.Close();
            gz.Close();
            fs.Close();
        }
        #endregion

        #region Public Redo
        public void Redo(DateTime since, Level l) { Redo(since.Ticks, l); }
        public void Redo(long since, Level l) {
            ExtraData<Tuple<short, short, short>, Tuple<long, byte>> toChange = new ExtraData<Tuple<short, short, short>, Tuple<long, byte>>();
            lock (lock_archive) {
                string path = GetFullPath(l);
                if (Directory.Exists(path)) {
                    string[] files = Directory.GetFiles(path, futureEnding);
                    List<string> toRedo = new List<string>();
                    foreach (string file in files) {
                        if (!file.EndsWith(futureEnding)) continue;
                        try {
                            long time = long.Parse(file.Split('.')[0]);
                            if (time >= since) toRedo.Add(file);
                        }
                        catch { }
                    }
                    foreach (string file in toRedo) {
                        try {
                            long time = long.Parse(file.Split('.')[0]);
                            FileStream fs = new FileStream(path + file, FileMode.Open, FileAccess.Read);
                            GZipStream gz = new GZipStream(fs, CompressionMode.Decompress);
                            MemoryStream ms = new MemoryStream();
                            int r = 0;
                            byte[] buffer = new byte[1024];
                            while ((r = gz.Read(buffer, 0, buffer.Length)) != -1) {
                                ms.Write(buffer, 0, r);
                            }
                            gz.Close();
                            fs.Close();
                            ms.Position = 0;
                            BinaryReader br = new BinaryReader(ms);
                            while (ms.Position < ms.Length) {
                                Tuple<short, short, short> v = new Tuple<short, short, short>(br.ReadInt16(), br.ReadInt16(), br.ReadInt16());
                                byte t = br.ReadByte();
                                if (toChange[v] == null || toChange[v].Item1 < time)
                                    toChange[v] = new Tuple<long, byte>(time, t);
                            }
                            br.Close();
                            ms.Close();
                        }
                        catch { }
                    }
                }
            }
            lock (lock_recent) {
                foreach (Tuple<short, short, short> v in toChange.Keys) {
                    byte currentBlock = l.GetBlock(v.Item1, v.Item2, v.Item3);
                    byte newBlock = toChange[v].Item2;
                    if (currentBlock != newBlock) {
                        recentTimes.Add(DateTime.Now.Ticks);
                        recentChanges.Add(new Tuple<Tuple<short,short,short> , byte, byte>(v, currentBlock, newBlock));
                        l.BlockChange(new Vector3S(v.Item1,v.Item2,v.Item3), newBlock); //TODO: Create BlockChange(short,short,short);
                    }
                }
            }
        }
        #endregion

        #region undo
        /// <summary>
        /// Returns all the blocks in the state they were before they got changed and removes them from history.
        /// </summary>
        /// <param name="since">The date time of the oldest blockchange</param>
        /// <returns>An ExtraData collection containing positions, times and types of the old blocks</returns>
        private ExtraData<Tuple<short, short, short>, Tuple<long, byte>> GetOriginalBlocks(long since, Level l) {
            ExtraData<Tuple<short, short, short>, Tuple<long, byte>> ret = new ExtraData<Tuple<short, short, short>, Tuple<long, byte>>();
            lock (lock_recent) {
                if (l == player.Level && recentTimes.Count > 0 && recentTimes[0] < since) {
                    int i;
                    for (i = recentTimes.Count - 1; recentTimes[i] >= since; i--) { //looping back in time
                        ret[recentChanges[i].Item1] = new Tuple<long, byte>(recentTimes[i], recentChanges[i].Item2); //if vector already exists it gets replaced else added
                    }
                    if (i < recentTimes.Count - 1) {
                        recentTimes.RemoveRange(i + 1, recentTimes.Count - 1 - i);
                        recentChanges.RemoveRange(i + 1, recentChanges.Count - 1 - i);
                    }
                    return ret; //this should later be passed to all other existing histories of level to redo their stuff during the undo timespawn
                }
            }
            lock (lock_recent) {
                if (l == player.Level) {
                    for (int i = recentTimes.Count - 1; i >= 0; i--) {
                        ret[recentChanges[i].Item1] = new Tuple<long, byte>(recentTimes[i], recentChanges[i].Item2);
                    }
                    recentTimes = new List<long>();
                    recentChanges = new List<Tuple<Tuple<short, short, short>, byte, byte>>();
                }
            }
            lock (lock_archive) {
                string path = GetFullPath(l);
                if (Directory.Exists(path)) {
                    Tuple<long, string> partialFile = null;
                    List<Tuple<long, string>> completeFiles = new List<Tuple<long, string>>();
                    //checking file times
                    foreach (string s in Directory.GetFiles(path, historyEnding)) {
                        if (s.EndsWith(historyEnding)) {
                            long filetime = 0;
                            try {
                                string time_str = s.Split('.')[0];
                                filetime = long.Parse(time_str);
                            }
                            catch {
                                continue;
                            }
                            if (filetime > since) {
                                completeFiles.Add(new Tuple<long, string>(filetime, s));
                            }
                            else if (filetime < since) {
                                if (partialFile != null && filetime > partialFile.Item1) partialFile = new Tuple<long, string>(filetime, s);
                            }
                            else {
                                //filetime == since -> there is no file containing only a few blockchanges
                                completeFiles.Add(new Tuple<long, string>(filetime, s));
                                partialFile = null;
                            }
                        }
                    }

                    //sorting files (newer first)
                    completeFiles.Sort((a, b) => { return a.Item1.CompareTo(b.Item1); });

                    //reading all files full of matching blockchanges
                    for (int i = 0; i < completeFiles.Count; i++) {
                        FileStream fs = new FileStream(path + completeFiles[i], FileMode.Open, FileAccess.Read);
                        GZipStream gz = new GZipStream(fs, CompressionMode.Decompress);
                        MemoryStream ms = new MemoryStream();
                        int r = 0;
                        byte[] buffer = new byte[1024];
                        while ((r = gz.Read(buffer, 0, buffer.Length)) != -1) {
                            ms.Write(buffer, 0, r);
                        }
                        gz.Close();
                        fs.Close();
                        if (ms.Length % 16 != 0) throw new Exception("Incomplete blockchange File: " + path + completeFiles[i]);
                        BinaryReader br = new BinaryReader(ms);
                        for (long pos = ms.Length - 16; pos >= 0; pos -= 16) { //start from end (newest entry is at end of file)
                            ms.Position = pos;
                            long time = br.ReadInt64();
                            short x = br.ReadInt16();
                            short z = br.ReadInt16();
                            short y = br.ReadInt16();
                            byte t = br.ReadByte(); //old block
                            ret[new Tuple<short, short, short>(x, z, y)] = new Tuple<long, byte>(time, t); //simple overwriting preexisting matches
                        }
                        File.Delete(path + completeFiles[i]);
                    }

                    //reading the partial file if not null
                    if (partialFile != null) {
                        FileStream fs = new FileStream(path + partialFile, FileMode.Open, FileAccess.Read);
                        GZipStream gz = new GZipStream(fs, CompressionMode.Decompress);
                        MemoryStream ms = new MemoryStream();
                        int r = 0;
                        byte[] buffer = new byte[1024];
                        while ((r = gz.Read(buffer, 0, buffer.Length)) != -1) {
                            ms.Write(buffer, 0, r);
                        }
                        buffer = null;
                        gz.Close();
                        fs.Close();
                        if (ms.Length % 16 != 0) throw new Exception("Incomplete .cbh-File: " + path + partialFile);
                        BinaryReader br = new BinaryReader(ms);
                        for (long pos = ms.Length - 16; pos >= 0; pos -= 16) {
                            ms.Position = pos;
                            long time = br.ReadInt64();
                            if (time < since) break; //other data will not match
                            short x = br.ReadInt16();
                            short z = br.ReadInt16();
                            short y = br.ReadInt16();
                            byte t = br.ReadByte();
                            ret[new Tuple<short, short, short>(x, z, y)] = new Tuple<long, byte>(time, t);
                        }
                        if (ms.Position > 0) {
                            int amount = (int)ms.Position;
                            ms.Position = 0;
                            FileStream overwrite = new FileStream(path + partialFile, FileMode.Create, FileAccess.Write);
                            GZipStream output = new GZipStream(overwrite, CompressionMode.Compress);
                            byte[] tmp = new byte[amount];
                            int t = 0;
                            while (t != tmp.Length) {
                                int _t = ms.Read(tmp, t, tmp.Length - t);
                                output.Write(tmp, t, tmp.Length - t);
                                t += _t;
                            }
                            output.Close();
                            overwrite.Close();
                        }
                        br.Close();
                        ms.Close();
                    }
                }
            }
            return ret;
        }
        #endregion

        #region undo others undo where others undo undid changes of this
        private ExtraData<Tuple<short, short, short>, Tuple<long, byte>> redoRecentOthersUndo(ExtraData<Tuple<short, short, short>, Tuple<long, byte>> ret, long since, Level l) {
            lock (lock_recent) {
                if (l == player.Level && recentTimes.Count > 0 && recentTimes[0] < since) {

                    int i;
                    for (i = 0; i < recentTimes.Count && recentTimes[i] < since; i++) ;
                    for (; i < recentTimes.Count; i++) { //looping forward in time
                        if (ret[recentChanges[i].Item1] != null && ret[recentChanges[i].Item1].Item1 < recentTimes[i]) {
                            ret[recentChanges[i].Item1] = new Tuple<long, byte>(recentTimes[i], recentChanges[i].Item3); //adding new block
                        }
                    }
                    return ret;
                }
                if (l == player.Level) {
                    for (int i = recentTimes.Count - 1; i >= 0; i--) {
                        ret[recentChanges[i].Item1] = new Tuple<long, byte>(recentTimes[i], recentChanges[i].Item2);
                    }
                }
            }
            return ret;
        }
        private static ExtraData<Tuple<short, short, short>, Tuple<long, byte>> redoArchiveOthersUndoForAllPlayers(ExtraData<Tuple<short, short, short>, Tuple<long, byte>> ret, long since, Level l) {
            lock (lock_archive) {
                string path = basepath + l + Path.DirectorySeparatorChar;
                if (Directory.Exists(path)) {
                    string[] dir = Directory.GetDirectories(path);
                    foreach (string playerPath in dir) {
                        //TODO: check for .. and .
                        Tuple<long, string> partialFile = null;
                        List<Tuple<long, string>> completeFiles = new List<Tuple<long, string>>();

                        //checking file times
                        foreach (string s in Directory.GetFiles(playerPath)) {
                            if (s.EndsWith(historyEnding)) {
                                long filetime = 0;
                                try {
                                    string time_str = s.Split('.')[0];
                                    filetime = long.Parse(time_str);
                                }
                                catch {
                                    continue;
                                }
                                if (filetime > since) {
                                    completeFiles.Add(new Tuple<long, string>(filetime, s));
                                }
                                else if (filetime < since) {
                                    if (partialFile != null && filetime > partialFile.Item1) partialFile = new Tuple<long, string>(filetime, s);
                                }
                                else {
                                    //filetime == since -> there is no file containing only a few blockchanges
                                    completeFiles.Add(new Tuple<long, string>(filetime, s));
                                    partialFile = null;
                                }
                            }
                        }

                        //sorting files (newer first)
                        completeFiles.Sort((a, b) => { return a.Item1.CompareTo(b.Item1); });

                        //reading the partial file if not null
                        if (partialFile != null) {
                            FileStream fs = new FileStream(playerPath + partialFile, FileMode.Open, FileAccess.Read);
                            GZipStream gz = new GZipStream(fs, CompressionMode.Decompress);
                            BinaryReader br = new BinaryReader(gz);
                            while (fs.Position < fs.Length) {
                                long time = br.ReadInt64();
                                if (time >= since) {
                                    short x = br.ReadInt16();
                                    short z = br.ReadInt16();
                                    short y = br.ReadInt16();
                                    br.ReadByte(); //old block is not needed
                                    byte t = br.ReadByte(); //new block
                                    Tuple<short, short, short> v = new Tuple<short, short, short>(x, z, y);
                                    if (ret[v] != null && ret[v].Item1 < time) ret[v] = new Tuple<long, byte>(time, t);

                                    break; //all other data will match now
                                }

                            }
                            while (fs.Position < fs.Length) {
                                long time = br.ReadInt64();
                                short x = br.ReadInt16();
                                short z = br.ReadInt16();
                                short y = br.ReadInt16();
                                br.ReadByte(); //drops old block
                                byte t = br.ReadByte();
                                Tuple<short, short, short> v = new Tuple<short, short, short>(x, z, y);
                                if (ret[v] != null && ret[v].Item1 < time) ret[v] = new Tuple<long, byte>(time, t);
                            }
                            br.Close();
                            gz.Close();
                            fs.Close();
                        }

                        //reading all files full of matching blockchanges
                        for (int i = 0; i < completeFiles.Count; i++) {
                            FileStream fs = new FileStream(playerPath + completeFiles[i], FileMode.Open, FileAccess.Read);
                            GZipStream gz = new GZipStream(fs, CompressionMode.Decompress);
                            BinaryReader br = new BinaryReader(gz);
                            while (fs.Position < fs.Length) {
                                long time = br.ReadInt64();
                                short x = br.ReadInt16();
                                short z = br.ReadInt16();
                                short y = br.ReadInt16();
                                br.ReadByte(); //drops old block
                                byte t = br.ReadByte(); //new block
                                Tuple<short, short, short> v = new Tuple<short, short, short>(x, z, y);
                                if (ret[v] != null && ret[v].Item1 < time) ret[v] = new Tuple<long, byte>(time, t);
                            }
                        }
                    }
                }
            }
            return ret;
        }
        #endregion

        /// <summary>
        /// Needs to be called before player changes level.
        /// </summary>
        private void WriteOut() {
            lock (lock_recent) {
                if (recentTimes.Count > 0) {
                    lock (lock_archive) { 
                        //this is a static lock! it could cause a deadlock if a function gets called between the lock(this.lock_recent) and
                        //lock(lock_archive) if and it locks lock_archive and awaits the unlock of this.lock_recent
                        //TODO: Don't lock(lock_archive) before lock(this.lock_recent) anywhere else!
                        string path = GetFullPath(player.Level);
                        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                        FileStream fs = new FileStream(recentTimes[0] + historyEnding, FileMode.Create, FileAccess.Write);
                        GZipStream zs = new GZipStream(fs, CompressionMode.Compress);
                        BinaryWriter bw = new BinaryWriter(zs);
                        for (int i = 0; i < recentTimes.Count; i++) { //writing old changes first because for each undo several redos need to be done
                            bw.Write(recentTimes[i]);//time
                            bw.Write(recentChanges[i].Item1.Item1);//coords
                            bw.Write(recentChanges[i].Item1.Item2);
                            bw.Write(recentChanges[i].Item1.Item3);
                            bw.Write(recentChanges[i].Item2);//old block
                            bw.Write(recentChanges[i].Item3);//new block
                        }
                        bw.Flush();
                        bw.Close();
                        zs.Flush();
                        zs.Close();
                        fs.Flush();
                        fs.Close();
                        recentTimes = new List<long>();
                        recentChanges = new List<Tuple<Tuple<short, short, short>, byte, byte>>();
                    }
                }
            }
        }
    }
}
