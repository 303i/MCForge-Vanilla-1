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
using System.Linq;
using System.Text;

namespace MCForge.Core
{
	public struct Point2
	{
		public ushort x;
		public ushort y;

		public Point2(ushort X, ushort Y)
		{
			x = X;
			y = Y;
		}
	}
	public struct Point3
	{
		public short x;
		public short y;
		public short z;

		public Point3(short X, short Z, short Y)
		{
			x = X;
			y = Y;
			z = Z;
		}
		public Point3(ushort X, ushort Z, ushort Y)
		{
			x = (short)X;
			y = (short)Y;
			z = (short)Z;
		}
	}

	public static class Colors
	{
		public const string black = "&0";
		public const string navy = "&1";
		public const string green = "&2";
		public const string teal = "&3";
		public const string maroon = "&4";
		public const string purple = "&5";
		public const string gold = "&6";
		public const string silver = "&7";
		public const string gray = "&8";
		public const string blue = "&9";
		public const string lime = "&a";
		public const string aqua = "&b";
		public const string red = "&c";
		public const string pink = "&d";
		public const string yellow = "&e";
		public const string white = "&f";

		public static string Parse(string str)
		{
			switch (str.ToLower())
			{
				case "black": return black;
				case "navy": return navy;
				case "green": return green;
				case "teal": return teal;
				case "maroon": return maroon;
				case "purple": return purple;
				case "gold": return gold;
				case "silver": return silver;
				case "gray": return gray;
				case "blue": return blue;
				case "lime": return lime;
				case "aqua": return aqua;
				case "red": return red;
				case "pink": return pink;
				case "yellow": return yellow;
				case "white": return white;
				default: return "";
			}
		}
		public static string Name(string str)
		{
			switch (str)
			{
				case black: return "black";
				case navy: return "navy";
				case green: return "green";
				case teal: return "teal";
				case maroon: return "maroon";
				case purple: return "purple";
				case gold: return "gold";
				case silver: return "silver";
				case gray: return "gray";
				case blue: return "blue";
				case lime: return "lime";
				case aqua: return "aqua";
				case red: return "red";
				case pink: return "pink";
				case yellow: return "yellow";
				case white: return "white";
				default: return "";
			}
		}
	}
}
