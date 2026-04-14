using CashFlow.Data.SqlDescriptors;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.ChatMethods;
using ECommons.GameFunctions;

namespace CashFlow.DataProvider;
public abstract unsafe class DataProviderBase
{
    public abstract void RecordPlayerCID(string playerName, uint homeWorld, ulong CID);
    public void RecordPlayerCID(IPlayerCharacter pc)
    {
        RecordPlayerCID(pc.Name.ToString(), pc.HomeWorld.RowId, pc.Struct()->ContentId);
    }
    public abstract Sender? GetPlayerInfo(ulong CID);
    public abstract void PurgeAllRecords(ulong CID);
    public abstract Dictionary<ulong, Sender> GetRegisteredPlayers();
    public abstract void RecordPlayerGil(GilRecordSqlDescriptor gilRecordSqlDescriptor);
    public abstract List<GilRecordSqlDescriptor> GetGilTimelineRecords(long unixTimeMsMin = 0, long unixTimeMsMax = 0);
}
