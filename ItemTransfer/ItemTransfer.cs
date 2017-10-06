#region Disclaimer
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
using System.IO.Streams;
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
using Terraria.ID;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using TShockAPI.DB;
using TShockAPI.Localization;
using Newtonsoft.Json;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;
#endregion

namespace ItemTransfer
{
    [ApiVersion(2, 1)]
    public class ItemTransfer : TerrariaPlugin
    {
        #region Info & Stuff
        public IDbConnection database;
        public String SavePath = TShock.SavePath;
        public TransferPlayer[] Playerlist = new TransferPlayer[256];
        internal static string filepath { get { return Path.Combine(TShock.SavePath, "transfer.json"); } }
        public override string Author { get { return "Tygra"; } }
        public override string Description { get { return "Item Transfer to alts"; } }
        public override string Name { get { return "Geldar Item Transfer"; } }
        public override Version Version { get { return new Version(1, 0); } }

        public ItemTransfer(Main game)
            : base(game)
        {

        }
        #endregion

        #region Initialize
        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("geldar.admin", TransferConfig.Reloadcfg, "transferreload"));
            Commands.ChatCommands.Add(new Command("geldar.transfer", Transfer, "transfer"));
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            if (!TransferConfig.ReadConfig())
            {
                TShock.Log.ConsoleError("Config loading failed. Consider deleting it.");
            }
        }
        #endregion

        #region Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }
        #endregion

        #region OnInitialize
        private void OnInitialize(EventArgs args)
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    database = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4}",
                        host[0],
                        host.Length == 1 ? "3306" : host[1],
                        TShock.Config.MySqlDbName,
                        TShock.Config.MySqlUsername,
                        TShock.Config.MySqlPassword)
                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "transfer.sqlite");
                    database = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;
            }

            SqlTableCreator sqlcreator = new SqlTableCreator(database, database.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            sqlcreator.EnsureTableStructure(new SqlTable("transfer",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("Sender", MySqlDbType.VarChar) { Length = 30 },
                new SqlColumn("Receiver", MySqlDbType.VarChar) { Length = 30 },
                new SqlColumn("Itemname", MySqlDbType.Text),
                new SqlColumn("ItemID", MySqlDbType.Int32),
                new SqlColumn("Stack", MySqlDbType.Int32),
                new SqlColumn("Active", MySqlDbType.Int32)
                ));
        }
        #endregion

        #region getItem
        private Item getItem(TSPlayer player, string itemNameOrId, int stack)
        {
            Item item = new Item();
            List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(itemNameOrId);
            if (matchedItems == null || matchedItems.Count == 0)
            {
                player.SendErrorMessage("Error: Incorrect item name or ID, please use quotes if the item has a space in it!");
                player.SendErrorMessage("Error: You have entered: {0}", itemNameOrId);
                return null;
            }
            else if (matchedItems.Count > 1)
            {
                TShock.Utils.SendMultipleMatchError(player, matchedItems.Select(i => i.Name));
                return null;
            }
            else
            {
                item = matchedItems[0];
            }
            if (stack > item.maxStack)
            {
                player.SendErrorMessage("Error: Stacks entered is greater then maximum stack size");
                return null;
            }
            return item;
        }
        #endregion

        #region Playerlist Join/Leave
        public void OnJoin(JoinEventArgs args)
        {
            Playerlist[args.Who] = new TransferPlayer(args.Who);
        }

        public void OnLeave(LeaveEventArgs args)
        {
            Playerlist[args.Who] = null;
        }
        #endregion

        #region Transfer
        private void Transfer(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendInfoMessage("Double qoutes around item names are required.");
                args.Player.SendInfoMessage("Available commands /transfer send/take/cancel/check.");
                return;
            }

            #region Transfer send
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "send")
            {

            }
            #endregion

            #region Transfer take
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "take")
            {

            }
            #endregion

            #region Transfer cancel
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "cancel")
            {

            }
            #endregion

            #region Transfer check
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "check")
            {

            }
            #endregion
        }
        #endregion
    }
}
