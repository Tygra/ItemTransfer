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
            int maxtransfers = TransferConfig.contents.maxactivetransfers;
            if (args.Parameters.Count < 1)
            {
                args.Player.SendInfoMessage("Double qoutes around item names are required.");
                args.Player.SendInfoMessage("If your alts name is more than one word, double qoutes are required there also.");
                args.Player.SendInfoMessage("Available commands /transfer send/take/cancel/check.");
                return;
            }

            #region Transfer send
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "send")
            {
                if (args.Parameters.Count == 1)
                {
                    args.Player.SendInfoMessage("Info: Double qoutes around item names are required.");
                    args.Player.SendInfoMessage("Info: If your alts name is more than one word, double qoutes are required there also.");
                    args.Player.SendInfoMessage("Info:    /transfer send \"your alts name\" \"item name\" stack");
                    args.Player.SendInfoMessage("Example: /transfer send \"Bad Boy\" \"Happy Grenade\" 22");
                    args.Player.SendInfoMessage("Info: You can only have a maximum of {0} active transfers.", maxtransfers);
                    return;
                }

                if (args.Parameters.Count == 4)
                {
                    QueryResult reader;
                    List<string> activetransfer = new List<string>();
                    reader = database.QueryReader("SELECT * FROM transfer WHERE Sender=@0 AND Active=@1;", args.Player.Name, 1);
                    if (reader.Read())
                    {
                        activetransfer.Add(reader.Get<string>("Username"));
                    }
                    if (activetransfer.Count < maxtransfers)
                    {
                        if (TransferConfig.contents.transferregions.Contains(args.Player.CurrentRegion.Name))
                        {
                            string sender = args.Player.Name;
                            string alt = string.Join(" ", args.Parameters[1]);
                            User userByName1 = TShock.Users.GetUserByName(sender);
                            User userByName2 = TShock.Users.GetUserByName(alt);
                            var altuuid = userByName1.UUID;
                            var senderuuid = userByName2.UUID;
                            if (alt != null && alt != "")
                            {
                                if (altuuid == senderuuid)
                                {
                                    int stack;
                                    if (!int.TryParse(args.Parameters[3], out stack))
                                    {
                                        args.Player.SendErrorMessage("Invalid stack size.");
                                        return;
                                    }
                                    if (stack <= 0)
                                    {
                                        args.Player.SendErrorMessage("Stack size can't be zero or less.");
                                        return;
                                    }
                                    Item item = getItem(args.Player, args.Parameters[2], stack);
                                    if (item == null)
                                    {
                                        return;
                                    }
                                    TSPlayer ply = args.Player;
                                    for (int i = 0; i < 50; i++)
                                    {
                                        if (ply.TPlayer.inventory[i].netID == item.netID)
                                        {
                                            if (ply.TPlayer.inventory[i].stack >= stack)
                                            {
                                                database.Query("INSERT INTO transfer(Sender, Receiver, Itemname, ItemID, Stack, Active) VALUES(@0, @1, @2, @3, @4, @5);", args.Player.Name, alt, item.Name, item.netID, stack, 1);
                                                if (ply.TPlayer.inventory[i].stack == stack)
                                                {
                                                    ply.TPlayer.inventory[i].SetDefaults(0);
                                                }
                                                else
                                                {
                                                    ply.TPlayer.inventory[i].stack -= stack;
                                                }
                                                NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, NetworkText.Empty, ply.Index, i);
                                                NetMessage.SendData((int)PacketTypes.PlayerSlot, ply.Index, -1, NetworkText.Empty, ply.Index, i);
                                                args.Player.SendInfoMessage("You set up {0} {1} for transfer to {2}.", stack, item.Name, alt);
                                                return;
                                            }
                                        }
                                    }
                                    args.Player.SendErrorMessage("You don't have the item or you don't have enough of it.");
                                    args.Player.SendErrorMessage("Item name provided: {0}. Stack: {1}.", item.Name, stack);
                                    return;
                                }
                                else
                                {
                                    args.Player.SendErrorMessage("This is not your alt. Name provided: {0}.", alt);
                                    return;
                                }
                            }
                            else
                            {
                                args.Player.SendErrorMessage("Invalid alt name. Name provided: {0}.", alt);
                                return;
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage("You are not in the right region to do the transfer.");
                            args.Player.SendErrorMessage("Valid region: Spawn");
                            return;
                        }
                    }
                    else
                    {
                        args.Player.SendErrorMessage("You have the maximum active transfers.");
                        args.Player.SendErrorMessage("Maximum active transfers for your rank: {0}.", maxtransfers);
                        return;
                    }
                }

                else
                {
                    args.Player.SendErrorMessage("Invalid syntax. Use /trade to get the correct syntax.");
                    return;
                }
            }
            #endregion

            #region Transfer take
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "take")
            {
                if (args.Parameters.Count == 2)
                {
                    string param1 = string.Join(" ", args.Parameters[1]);
                    var id = Convert.ToInt32(param1);
                    List<string> sendr = new List<string>();
                    List<string> receivr = new List<string>();
                    List<string> itemName = new List<string>();
                    List<int> itemid = new List<int>();
                    List<int> amount = new List<int>();
                    if (id <= 0)
                    {
                        args.Player.SendErrorMessage("ID can't be zero or less.");
                        return;
                    }
                    if (args.Player.InventorySlotAvailable)
                    {
                        using (var reader = database.QueryReader("SELECT * FROM transfer WHERE ID=@0 AND Receiver=@1 Active=@2;", id, args.Player.Name, 1))
                        {
                            bool read = false;
                            while (reader.Read())
                            {
                                read = true;
                                sendr.Add(reader.Get<string>("Sender"));
                                receivr.Add(reader.Get<string>("Receiver"));
                                itemName.Add(reader.Get<string>("Itemname"));
                                itemid.Add(reader.Get<int>("ItemID"));
                                amount.Add(reader.Get<int>("Stack"));
                            }
                            if (!read)
                            {
                                args.Player.SendErrorMessage("Invalid ID or the transfer has been already claimed.");
                                args.Player.SendErrorMessage("ID provided: {0}.", id);
                                return;
                            }
                        }
                        string sender = sendr.FirstOrDefault();
                        string receiver = receivr.FirstOrDefault();
                        string itemname = itemName.FirstOrDefault();
                        int itemID = itemid.FirstOrDefault();
                        int stack = amount.FirstOrDefault();
                        if (receiver != args.Player.Name)
                        {
                            database.Query("UPDATE transfer SET Active=@0 WHERE ID=@1;", 0, id);
                            Item ItemById = TShock.Utils.GetItemById(itemID);
                            args.Player.GiveItem(ItemById.type, ItemById.Name, ItemById.width, ItemById.height, stack, 0);
                            args.Player.SendInfoMessage("Transfer of {0} {1} with the ID {2} has been accepted, and delivered.", stack, itemname, id);
                        }
                    }
                    else
                    {
                        args.Player.SendErrorMessage("Your inventory seems to be full. Free up one slot, and try again.");
                        return;
                    }
                }
                else
                {
                    args.Player.SendErrorMessage("Invalid syntax. You can only take on transfer at a time.");
                    args.Player.SendErrorMessage("Get the ID from /transfer check.");
                    return;
                }
            }
            #endregion

            #region Transfer cancel
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "cancel")
            {
                if (args.Parameters.Count == 2)
                {
                    string param1 = string.Join(" ", args.Parameters[1]);
                    var id = Convert.ToInt32(param1);
                    List<string> selfcheck = new List<string>();
                    List<int> itemID = new List<int>();
                    List<int> amount = new List<int>();
                    List<string> itemname = new List<string>();
                    if (id <= 0)
                    {
                        args.Player.SendErrorMessage("ID can't be zero or less.");
                        return;
                    }
                    if (args.Player.InventorySlotAvailable)
                    {
                        using (var reader = database.QueryReader("SELECT * FROM transfer WHERE ID=@0 AND Active=@1;", id, 1))
                        {
                            bool read = false;
                            while (reader.Read())
                            {
                                read = true;
                                selfcheck.Add(reader.Get<string>("Sender"));
                                itemID.Add(reader.Get<int>("ItemID"));
                                amount.Add(reader.Get<int>("Stack"));
                                itemname.Add(reader.Get<string>("Itemname"));
                            }
                            if (!read)
                            {
                                args.Player.SendErrorMessage("ID is nott valid. ID provided: {0}.", id);
                                return;
                            }
                        }
                    }
                    else
                    {
                        args.Player.SendErrorMessage("Your inventory seems to be full. Free up one slot, and try again.");
                        return;
                    }
                }
                else
                {
                    args.Player.SendErrorMessage("Invalid syntax. Use /transfer cancel ID.");
                    args.Player.SendErrorMessage("Get the ID from /transfer check.");
                    return;
                }
            }
            #endregion

            #region Transfer check
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "check")
            {
                int pageNumber;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                {
                    return;
                }
                List<string> check = new List<string>();
                using (var reader = database.QueryReader("SELECT * FROM transfer WHERE Sender=@0 AND Active=@1;", args.Player.Name, 1))
                {
                    bool read = false;
                    while (reader.Read())
                    {
                        check.Add(String.Format("{0}" + " - " + "{1}" + " - " + "{2}" + " - " + "{3}", reader.Get<int>("ID"), reader.Get<string>("Receiver"), reader.Get<string>("Itemname"), reader.Get<int>("Stack")));
                        read = true;
                    }
                    if (!read)
                    {
                        args.Player.SendErrorMessage("You don't have any active transfers.");
                        return;
                    }
                }
                PaginationTools.SendPage(args.Player, pageNumber, check,
                    new PaginationTools.Settings
                    {
                        MaxLinesPerPage = 5,
                        HeaderFormat = "ID - Receiver - Itemname - Stack ({0}/{1})",
                        FooterFormat = "Type {0}transfer check {{0}} for more.".SFormat(Commands.Specifier)
                    });
            }
            #endregion
        }
        #endregion
    }
}
