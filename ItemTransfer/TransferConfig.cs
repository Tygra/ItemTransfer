﻿#region Disclaimer
/*  
 *  The plugin has some features that I got from other authors.
 *  I don't claim any ownership over those elements which were made by someone else.
 *  The plugin has been customized to fit our need at Geldar,
 *  and because of this, it's useless for anyone else.
 *  I know timers are shit, and If someone knows a way to keep them after relog, tell me.
*/
#endregion

#region Refs
using System;
using System.Data;
using System.IO;
using System.ComponentModel;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

//Terraria related refs
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Localization;
using Newtonsoft.Json;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
#endregion

namespace ItemTransfer
{
    public class TransferConfig
    {
        public static Contents contents;

        #region Config create
        public static void CreateConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "transfer.json");
            try
            {
                using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using (var sr = new StreamWriter(stream))
                    {
                        contents = new Contents();
                        var configString = JsonConvert.SerializeObject(contents, Formatting.Indented);
                        sr.Write(configString);
                    }
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
        }
        #endregion

        #region Config Read
        public static bool ReadConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "transfer.json");
            try
            {
                if (File.Exists(filepath))
                {
                    using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            var configString = sr.ReadToEnd();
                            contents = JsonConvert.DeserializeObject<Contents>(configString);
                        }
                        stream.Close();
                    }
                    return true;
                }
                else
                {
                    TShock.Log.ConsoleError("Transfer config not found, how about a new one?");
                    CreateConfig();
                    return true;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
            return false;
        }
        #endregion

        #region Config
        public class Contents
        {
            public int maxactivetransfers = 5;
            public String[] transferregions = { "spawn" };
        }
        #endregion

        #region Config reload
        public static void Reloadcfg(CommandArgs args)
        {
            if (ReadConfig())
            {
                args.Player.SendMessage("Transfer config reloaded", Color.Goldenrod);
            }
            else
            {
                args.Player.SendErrorMessage("Something went wrong.");
            }
        }
        #endregion
    }
}
