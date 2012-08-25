﻿/*
Copyright 2011 MCForge
Dual-licensed under the Educational Community License, Version 2.0 and
the GNU General Public License, Version 3 (the "Licenses"); you may
not use this file except in compliance with the Licenses. You may
obtain a copy of the Licenses at
http://www.opensource.org/licenses/ecl2.php
http://www.gnu.org/licenses/gpl-3.0.html
Unless required by applicable law or agreed to in writing,
software distributed under the Licenses are distributed on an "AS IS"
BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
or implied. See the Licenses for the specific language governing
permissions and limitations under the Licenses.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MCForge.Core;
using MCForge.Entity;
using MCForge.Interface.Command;
using MCForge.Utils;
using MCForge.World;

namespace MCForge.Groups
{
    /// <summary>
    /// All the default permission values
    /// </summary>
    /// <remarks></remarks>
    public enum PermissionLevel : byte
    {
        /// <summary>
        /// Guest permission
        /// <value>0</value>
        /// </summary>
        Guest = 0,

        /// <summary>
        /// Builder permission
        /// <value>30</value>
        /// </summary>
        Builder = 30,

        /// <summary>
        /// Advanced Builder permission
        /// <value>50</value>
        /// </summary>
        AdvBuilder = 50,

        /// <summary>
        /// Operator permission
        /// <value>80</value>
        /// </summary>
        Operator = 80,

        /// <summary>
        /// Super Operator permission
        /// <value>100</value>
        /// </summary>
        SuperOP = 100,

        /// <summary>
        /// Owner permission
        /// <value>120</value>
        /// </summary>
        Owner = 120
    }

    /// <summary>
    /// The main permission system for MCForge
    /// </summary>
    /// <remarks></remarks>
    public class PlayerGroup
    {
        /// <summary>
        /// A list of all the available groups 
        /// </summary>
        public static List<PlayerGroup> Groups = new List<PlayerGroup>();

        public static PlayerGroup Default;

        /// <summary>
        /// A list of all the players in the group (includes offline players)
        /// </summary>
        public List<string> Players = new List<string>();

        private string _file;
        private int _maxblockchange = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerGroup"/> class. without adding it to the group list
        /// </summary>
        /// <remarks></remarks>
        internal PlayerGroup()
        {
            Name = Colour = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerGroup"/> class.
        /// </summary>
        /// <param name="perm">The permission level of the group.</param>
        /// <param name="name">The name of the group.</param>
        /// <param name="colour">The colour of the group.</param>1
        /// <param name="file">The filename to save the group player list in</param>
        /// <remarks></remarks>
        public PlayerGroup(int perm, string name, string colour, string file)
        {
            if (Groups.ToArray().Any(g => name.ToLower() == g.Name.ToLower()))
            {
                throw new ArgumentException("Cannot have 2 groups of the same name");
            }

            string file1 = "ranks/" + file;
            if (!Directory.Exists(Path.GetDirectoryName(file1)))
                Directory.CreateDirectory(Path.GetDirectoryName(file1));
            if (!System.IO.File.Exists(file1))
            {
                System.IO.File.Create(file1).Close();
                Logger.Log("[Groups] " + file + " was created", System.Drawing.Color.DarkGreen,
                           System.Drawing.Color.Black);
            }
            try
            {
                Permission = byte.Parse(perm.ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
                throw new ArgumentException("Permission has to be above 0 and below 255");
            }
            Name = name;
            Colour = colour;
            File = file;

            LoadGroup();
            Groups.Add(this);
        }

        /// <summary>
        /// The name of the group
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The permission level of the group.
        /// </summary>
        public byte Permission { get; set; }

        /// <summary>
        /// The colour of the group.
        /// note color == colour
        /// </summary>
        public string Colour
        {
            get { return Color; }
            set { Color = value; }
        }

        /// <summary>
        /// The color of the group.
        /// note color == colour
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// The maximum amount of blocks this group can change.
        /// </summary>
        public int MaxBlockChange
        {
            get { return _maxblockchange; }
            set { _maxblockchange = value; }
        }

        /// <summary>
        /// The filename to save the group list into. when setting it des not contain the "ranks/" part
        /// </summary>
        public string File
        {
            get { return "ranks/" + _file; }
            set { _file = value; }
        }

        /// <summary>
        /// Adds this instance to the list of groups ONLY use it when you initialised with PlayerGroup().
        /// </summary>
        /// <remarks></remarks>
        internal void Add()
        {
            if (Name != null && Colour != null)
            {
                if (Groups.ToArray().Any(g => Name.ToLower() == g.Name.ToLower()))
                {
                    throw new ArgumentException("Cannot have 2 groups of the same name");
                }

                string file1 = File;
                if (!Directory.Exists(Path.GetDirectoryName(file1)))
                    Directory.CreateDirectory(Path.GetDirectoryName(file1));
                if (!System.IO.File.Exists(file1))
                {
                    System.IO.File.Create(file1).Close();
                    Logger.Log("[Groups] " + File + " was created", System.Drawing.Color.DarkGreen,
                               System.Drawing.Color.Black);
                }
                try
                {
                    Permission = byte.Parse(Permission.ToString(CultureInfo.InvariantCulture));
                }
                catch
                {
                    throw new ArgumentException("Permission has to be above 0 and below 255");
                }

                LoadGroup();
                Groups.Add(this);
            }
            else
            {
                throw new Exception("Incomplete group, skipping add");
            }
        }

        /// <summary>
        /// Sends the message to the group.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <remarks></remarks>
        public void SendMessage(string message)
        {
            Server.ForeachPlayer(delegate(Player p)
                                     {
                                         if (p.Group == this)
                                             p.SendMessage(message);
                                     });
        }

        /// <summary>
        /// Determines whether this instance can execute the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns><c>true</c> if this instance can execute the specified command; otherwise, <c>false</c>.</returns>
        public bool CanExecute(ICommand command)
        {
            if (CommandPermissionOverrides.overrides.ContainsKey(command))
                if (CommandPermissionOverrides.overrides.Any(cmd => cmd.Key == command && cmd.Value <= Permission))
                {
                    return true;
                }

            return command.Permission <= Permission && !CommandPermissionOverrides.overrides.ContainsKey(command);
        }

        /// <summary>
        /// Determines whether this instance can build the specified block.
        /// </summary>
        /// <param name="b">The block.</param>
        /// <returns><c>true</c> if this instance can build the specified block; otherwise, <c>false</c>.</returns>
        public bool CanBuild(Block b)
        {
            return b.Permission <= Permission;
        }

        /// <summary>
        /// Saves the group to disk.
        /// </summary>
        /// <returns><c>true</c> if no errors saving</returns>
        /// <remarks></remarks>
        public bool SaveGroup()
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(File)))
                    Directory.CreateDirectory(Path.GetDirectoryName(File));

                TextWriter o = new StreamWriter(File);
                foreach (string s in Players.ToArray())
                    o.WriteLine(s.ToLower());
                o.Flush();
                o.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Loads the group from disk.
        /// </summary>
        /// <returns><c>true</c> if no errors when loading</returns>
        /// <remarks></remarks>
        public bool LoadGroup()
        {
            try
            {
                string line;
                TextReader file = new StreamReader(File);

                while ((line = file.ReadLine()) != null)
                {
                    if (!string.IsNullOrEmpty(line))
                        if (!Players.Contains(line.ToLower()))
                            Players.Add(line.ToLower());
                }
                file.Close();
                file.Dispose();
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Adds the player to this group.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <remarks></remarks>
        public void AddPlayer(Player p)
        {
            p.Group.Players.Remove(p.Username.ToLower());
            p.Group = this;
            Players.Add(p.Username.ToLower());
            SaveGroup();
        }

        /// <summary>
        /// Loads this groups player list.
        /// </summary>
        /// <remarks></remarks>
        public static void Load()
        {
            CommandPermissionOverrides.Load();

            Groups.Clear();
            InitDefaultGroups();

            foreach (PlayerGroup g in Groups)
            {
                Logger.Log("[Group] " + g.Name + " Initialized", LogType.Debug);
            }
        }

        /// <summary>
        /// Initializes the default groups.
        /// </summary>
        /// <remarks></remarks>
        public static void InitDefaultGroups()
        {
            if (FileUtils.FileExists(FileUtils.PropertiesPath + "groups.xml"))
                PlayerGroupProperties.Load();
            if (Groups != null && Groups.Count <= 0)
            {
                new PlayerGroup((byte) PermissionLevel.Guest, "Guest", Colors.white, "guests.txt");
                new PlayerGroup((byte) PermissionLevel.Builder, "Builder", Colors.green, "builders.txt");
                new PlayerGroup((byte) PermissionLevel.AdvBuilder, "AdvBuilder", Colors.lime, "advbuilders.txt");
                new PlayerGroup((byte) PermissionLevel.Operator, "Operator", Colors.purple, "ops.txt");
                new PlayerGroup((byte) PermissionLevel.SuperOP, "SuperOp", Colors.maroon, "superops.txt");
                new PlayerGroup((byte) PermissionLevel.Owner, "Owner", Colors.blue, "owners.txt");
                PlayerGroupProperties.Save();
            }
        }

        /// <summary>
        /// Checks if the specified group exists
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static bool Exists(string name)
        {
            return Groups.Any(g => g.Name.ToLower() == name.ToLower());
        }

        /// <summary>
        /// Finds the specified group.
        /// </summary>
        /// <param name="name">The name of the group.</param>
        public static PlayerGroup Find(string name)
        {
            if (name == "adv" && !Exists(name)) name = "advbuilder";
            else if (name == "op" && !Exists(name)) name = "operator";
            else if (name == "admin" && !Exists(name)) name = "superop";

            return Groups.FirstOrDefault(g => g.Name.ToLower() == name.ToLower());
        }

        /// <summary>
        /// Finds the specified group whose permission is the given int.
        /// </summary>
        /// <param name="perm">The permission of the group.</param>
        /// <returns></returns>
        public static PlayerGroup FindPermInt(int perm)
        {
            return Groups.FirstOrDefault(g => g.Permission == perm);
        }

        public static implicit operator byte(PlayerGroup group)
        {
            return group.Permission;
        }
        public static implicit operator string(PlayerGroup group)
        {
            return group.Name;
        }
        public override string ToString()
        {
            return this; //because of the implicit string operator
        }
    }
}