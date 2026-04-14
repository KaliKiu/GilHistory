using CashFlow.Data.SqlDescriptors;
using Dapper;
using ECommons.ChatMethods;
using ECommons.GameHelpers;
using SqlKata.Execution;
using System.IO;
using System.Xml.Linq;

namespace CashFlow.DataProvider.Sqlite;

public unsafe class SqliteDataProvider : DataProviderBase
{
    private static string GilHistoryDirectory => Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "gil-history");
    private static string GilHistoryXmlFile => Path.Combine(GilHistoryDirectory, "gil-history.xml");

    public SqliteDataProvider()
    {
        using var db = new GilsightQueryFactory();
        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.CidMap} (
                    Cid       NUMERIC UNIQUE,
                    Name      TEXT NOT NULL,
                    HomeWorld INTEGER NOT NULL
                );
                """);

        db.Connection.Execute($"""
                CREATE TABLE IF NOT EXISTS {Tables.GilTimelineRecords} (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    Cid         NUMERIC NOT NULL,
                    GilPlayer   NUMERIC NOT NULL,
                    GilRetainer NUMERIC NOT NULL,
                    UnixTime    NUMERIC NOT NULL
                );
                """);

        db.Connection.Execute($"""
                CREATE INDEX IF NOT EXISTS IX_GilTimeline_Cid_UnixTime
                ON {Tables.GilTimelineRecords} (Cid, UnixTime);
                """);

        EnsureGilHistoryXmlExists();
    }

    public override Sender? GetPlayerInfo(ulong CID)
    {
        using var db = new GilsightQueryFactory();
        var longCid = *(long*)&CID;
        var result = db.Query(Tables.CidMap).Where("Cid", longCid).Get();
        if(result.Any())
        {
            var f = result.First();
            return new(f.Name, (uint)f.HomeWorld);
        }
        return null;
    }

    public override void PurgeAllRecords(ulong CID)
    {
        var cidLong = *(long*)&CID;
        S.WorkerThread.ClearAndEnqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            var result = db.Query(Tables.GilTimelineRecords).Where("Cid", "=", cidLong).Delete();
            PluginLog.Information($"Removed {result} entries from {Tables.GilTimelineRecords}");
        });
    }

    public override void RecordPlayerCID(string playerName, uint homeWorld, ulong CID)
    {
        var longCid = *(long*)&CID;
        S.WorkerThread.Enqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            if(db.Query(Tables.CidMap).Where("Cid", longCid).Exists())
            {
                db.Query(Tables.CidMap).Where("Cid", longCid).Update([new("Name", playerName), new("HomeWorld", (int)homeWorld)]);
            }
            else
            {
                db.Query(Tables.CidMap).Insert([new("Cid", longCid), new("Name", playerName), new("HomeWorld", (int)homeWorld)]);
            }
        });
    }

    public override Dictionary<ulong, Sender> GetRegisteredPlayers()
    {
        var ret = new Dictionary<ulong, Sender>();
        using var db = new GilsightQueryFactory();
        foreach(var x in db.Query(Tables.CidMap).Get())
        {
            var cid = (long)x.Cid;
            var name = (string)x.Name;
            var homeWorld = (uint)x.HomeWorld;
            ret[*(ulong*)&cid] = new(name, homeWorld);
        }
        return ret;
    }

    public override void RecordPlayerGil(GilRecordSqlDescriptor gilRecordSqlDescriptor)
    {
        if(C.Blacklist.Contains(Player.CID)) return;
        S.WorkerThread.Enqueue(() =>
        {
            using var db = new GilsightQueryFactory();
            db.Query(Tables.GilTimelineRecords).Insert(new GilRecordSqlDescriptor
            {
                CidUlong = gilRecordSqlDescriptor.CidUlong,
                GilPlayer = Math.Max(0, gilRecordSqlDescriptor.GilPlayer),
                GilRetainer = Math.Max(0, gilRecordSqlDescriptor.GilRetainer),
                UnixTime = gilRecordSqlDescriptor.UnixTime
            });
            SaveGilRecordToXml(gilRecordSqlDescriptor);
            S.MainWindow.UpdateData(false);
        });
    }

    public override List<GilRecordSqlDescriptor> GetGilTimelineRecords(long unixTimeMsMin = 0, long unixTimeMsMax = 0)
    {
        using var db = new GilsightQueryFactory();
        var result = db.Query(Tables.GilTimelineRecords);
        if(unixTimeMsMin > 0) result = result.Where("UnixTime", ">=", unixTimeMsMin);
        if(unixTimeMsMax > 0) result = result.Where("UnixTime", "<=", unixTimeMsMax);
        result = result.OrderBy("UnixTime");
        return [.. result.Get<GilRecordSqlDescriptor>()];
    }

    private static void EnsureGilHistoryXmlExists()
    {
        Directory.CreateDirectory(GilHistoryDirectory);
        if(File.Exists(GilHistoryXmlFile)) return;

        var doc = new XDocument(new XElement("GilHistory"));
        doc.Save(GilHistoryXmlFile);
    }

    private static void SaveGilRecordToXml(GilRecordSqlDescriptor record)
    {
        EnsureGilHistoryXmlExists();
        var doc = XDocument.Load(GilHistoryXmlFile);
        var root = doc.Root;
        if(root == null)
        {
            root = new XElement("GilHistory");
            doc.Add(root);
        }

        root.Add(new XElement("Entry",
            new XAttribute("cid", $"{record.CidUlong:X16}"),
            new XAttribute("gilPlayer", Math.Max(0, record.GilPlayer)),
            new XAttribute("gilRetainer", Math.Max(0, record.GilRetainer)),
            new XAttribute("totalGil", Math.Max(0, record.GilPlayer) + Math.Max(0, record.GilRetainer)),
            new XAttribute("unixTimeMs", record.UnixTime),
            new XAttribute("localTime", DateTimeOffset.FromUnixTimeMilliseconds(record.UnixTime).ToLocalTime().ToString("O"))
        ));
        doc.Save(GilHistoryXmlFile);
    }
}
