﻿/*
	Copyright 2011 MCForge
		
	Dual-licensed under the	Educational Community License, Version 2.0 and
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
using System.IO;
using System.Collections.Generic;
//using System.Timers;
using System.Text;
using System.Text.RegularExpressions;
using Sharkbite.Irc;
//using System.Threading;

namespace MCForge {
    public class GlobalChatBot {
        public delegate void RecieveChat(string nick, string message);
        public static event RecieveChat OnNewRecieveGlobalMessage;

        public delegate void SendChat(string player, string message);
        public static event SendChat OnNewSayGlobalMessage;

        public delegate void KickHandler(string reason);
        public event KickHandler OnGlobalKicked;

        private Connection connection;
        private string server, channel, nick;
        private bool reset = false;
        private byte retries = 0;

        const string caps = "ABCDEFGHIJKLMNOPQRSTUVWXYZ ";
        const string nocaps = "abcdefghijklmnopqrstuvwxyz ";
        static readonly List<string> extensions = new List<string>();
        public GlobalChatBot(string nick) {
            /*if (!File.Exists("Sharkbite.Thresher.dll"))
            {
                Server.UseGlobalChat = false;
                Server.s.Log("[GlobalChat] The IRC dll was not found!");
                return;
            }*/
            server = "irc.geekshed.net"; channel = "#MCForge"; this.nick = nick.Replace(" ", "");
            connection = new Connection(new ConnectionArgs(nick, server), false, false);
            if (Server.UseGlobalChat) {
                // Regster events for incoming
                connection.Listener.OnNickError += new NickErrorEventHandler(Listener_OnNickError);
                connection.Listener.OnRegistered += new RegisteredEventHandler(Listener_OnRegistered);
                connection.Listener.OnPublic += new PublicMessageEventHandler(Listener_OnPublic);
                connection.Listener.OnJoin += new JoinEventHandler(Listener_OnJoin);
                connection.Listener.OnKick += new KickEventHandler(Listener_OnKick);
                connection.Listener.OnError += new ErrorMessageEventHandler(Listener_OnError);
                connection.Listener.OnDisconnected += new DisconnectedEventHandler(Listener_OnDisconnected);
            }
        }
        public void Say(string message, Player p = null) {
            if (p != null && p.muted) {
                Player.SendMessage(p, "*Tears* You aren't allowed to talk to the nice people of global chat");
            }
            if (p != null ? !p.canusegc : Server.canusegc) { p.SendMessage("You can no longer use the GC!"); return; }
            if (message.Contains("minecraft.net/classic/play/")) { if (p != null) { Player.SendMessage(p, "No server links Mr whale!"); p.multi++; Command.all.Find("gcrules").Use(p, ""); } return; }
            if (message.Contains("http://") || message.Contains("https://") || message.Contains("www.")) { if (p != null) { p.SendMessage("No links!"); p.multi++; Command.all.Find("gcrules").Use(p, ""); } return; }
            if (Player.HasBadColorCodes(message)) { if (p != null)Player.SendMessage(p, "Can't let you do that Mr whale!"); if (p != null) { p.Kick("Kicked for trying to crash all!"); } return; }
            foreach (string i in extensions) { if (message.ToLower().Contains(i)) { if (p != null) { p.SendMessage("No links?!"); p.multi++; } return; } }
            if (message.ToLower().Contains(Server.name.ToLower())) { if (p != null) { Player.SendMessage(p, "Let's not advertise Mr whale!"); p.multi++; } return; }

            #region Repeating message spam
            if (p.lastmsg == message.ToLower()) {
                p.spamcount++;
                p.multi++;
                p.SendMessage("Don't send repetitive messages!");
                if (p.spamcount >= 4) { p.canusegc = false; p.SendMessage("You can no longer use the gc! Reason: repetitive message spam"); return; }
                if (p.spamcount >= 2) { return; }
                return;
            }
            else { p.lastmsg = message.ToLower(); p.spamcount = 0; }
            #endregion

            #region Caps spam
            int rage = 0;
            for (int i = 0; i < message.Length; i++) { if (caps.IndexOf(message[i]) != -1) { rage++; } }
            if (rage >= 7 && !(message.Length <= 7)) { //maybe more if there are still people who use proper capitalization?
                p.SendMessage("Woah there Mr whale, lay of the caps!");
                message = message.ToLower();
                p.capscount++;
                p.multi++;
                if (p.capscount >= 5) { p.canusegc = false; p.SendMessage("You can no longer use the gc! Reason: caps spam"); return; }
                if (rage >= 10) { return; }
            }
            #endregion

            #region Flooding
            TimeSpan t = DateTime.Now - p.lastmsgtime;
            if (t < new TimeSpan(0, 0, 1)) {
                p.SendMessage("Stop the flooding buddy!");
                p.floodcount++;
                p.multi++;
                if (p.floodcount >= 5) { p.canusegc = false; p.SendMessage("You can no longer use the gc! Reason: flooding"); }
                if (p.floodcount >= 3) { return; }
            }
            p.lastmsgtime = DateTime.Now;
            #endregion

            if (p.multi >= 10) { p.canusegc = false; p.SendMessage("You can no longer use the gc! Reason: multiple offenses!"); return; }
            if (OnNewSayGlobalMessage != null)
                OnNewSayGlobalMessage(p == null ? "Console" : p.name, message);
            if (Server.UseGlobalChat && IsConnected())
                connection.Sender.PublicMessage(channel, message);
        }
        public void Pm(string user, string message) {
            if (Server.UseGlobalChat && IsConnected())
                connection.Sender.PrivateMessage(user, message);
        }
        public void Reset() {
            if (!Server.UseGlobalChat) return;
            reset = true;
            retries = 0;
            Disconnect("Global Chat bot resetting...");
            Connect();
        }

        void Listener_OnJoin(UserInfo user, string channel) {
            if (user.Nick == nick)
                Server.s.Log("Joined the Global Chat!");
        }

        void Listener_OnError(ReplyCode code, string message) {
            //Server.s.Log("IRC Error: " + message);
        }

        void Listener_OnPublic(UserInfo user, string channel, string message) {
            //string allowedchars = "1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./!@#$%^*()_+QWERTYUIOPASDFGHJKL:\"ZXCVBNM<>? ";
            //string msg = message;
            if (message.Contains("^UGCS")) {
                Server.UpdateGlobalSettings();
                return;
            }
            if (message.Contains("^IPGET ")) {
                foreach (Player p in Player.players) {
                    if (p.name == message.Split(' ')[1]) {
                        if (Server.UseGlobalChat && IsConnected()) {
                            connection.Sender.PublicMessage(channel, "^IP " + p.name + ": " + p.ip);
                        }
                    }
                }
            }
            if (message.Contains("^GETINFO ")) {
                if (Server.GlobalChatNick == message.Split(' ')[1]) {
                    if (Server.UseGlobalChat && IsConnected()) {
                        connection.Sender.PublicMessage(channel, "^NAME: " + Server.name);
                        connection.Sender.PublicMessage(channel, "^MOTD: " + Server.motd);
                        connection.Sender.PublicMessage(channel, "^VERSION: " + Server.version);
                        connection.Sender.PublicMessage(channel, "^GLOBAL NAME: " + Server.GlobalChatNick);
                        connection.Sender.PublicMessage(channel, "^URL: " + Server.URL);
                        connection.Sender.PublicMessage(channel, "^PLAYERS: " + Player.players.Count + "/" + Server.players);
                    }
                }
            }
            if (message.StartsWith("^")) { return; }
            message = message.MCCharFilter();
            if (Player.MessageHasBadColorCodes(null, message))
                return;
            if (OnNewRecieveGlobalMessage != null) {
                OnNewRecieveGlobalMessage(user.Nick, message);
            }
            if (Server.devs.Contains(message.Split(':')[0]) && !message.StartsWith("[Dev]") && !message.StartsWith("[Developer]")) { message = "[Dev]" + message; }
            /*try { 
                if(GUI.GuiEvent != null)
                GUI.GuiEvents.GlobalChatEvent(this, "> " + user.Nick + ": " + message); }
            catch { Server.s.Log(">[Global] " + user.Nick + ": " + message); }*/
            Player.GlobalMessage(String.Format("{0}>[Global] {1}: &f{2}", Server.GlobalChatColor, user.Nick, Server.profanityFilter ? ProfanityFilter.Parse(message) : message), true);
        }

        void Listener_OnRegistered() {
            reset = false;
            retries = 0;
            connection.Sender.Join(channel);
        }

        void Listener_OnDisconnected() {
            if (!reset && retries < 5) { retries++; Connect(); }
        }

        void Listener_OnNickError(string badNick, string reason) {
            Server.s.Log("Global Chat nick \"" + badNick + "\" is  taken, please choose a different nick.");
        }

        void Listener_OnKick(UserInfo user, string channel, string kickee, string reason) {
            if (kickee.Trim().ToLower() == nick.ToLower()) {
                Server.s.Log("Kicked from Global Chat: " + reason);
                if (OnGlobalKicked != null) OnGlobalKicked(reason);
                Server.s.Log("Attempting to rejoin...");
                connection.Sender.Join(channel);
            }

        }

        public void Connect() {
            if (!Server.UseGlobalChat || Server.shuttingDown) return;
            try { connection.Connect(); }
            catch { }
        }

        public void Disconnect(string reason) {
            if (Server.UseGlobalChat && IsConnected()) { connection.Disconnect(reason); Server.s.Log("Disconnected from Global Chat!"); }
        }

        public bool IsConnected() {
            if (!Server.UseGlobalChat) return false;
            try { return connection.Connected; }
            catch { return false; }
        }
    }
}
