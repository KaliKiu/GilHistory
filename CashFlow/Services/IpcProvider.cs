using CashFlow.Data.SqlDescriptors;
using ECommons.ChatMethods;
using ECommons.EzIpcManager;
using System;
using System.Collections.Generic;
using System.Text;

namespace CashFlow.Services;

public class IpcProvider
{
    private IpcProvider()
    {
        EzIPC.Init(this);
    }

    [EzIPC]
    public List<GilRecordSqlDescriptor> GetGilRecords(long unixTimeMsMin, long unixTimeMsMax)
    {
        return P.DataProvider.GetGilTimelineRecords(unixTimeMsMin, unixTimeMsMax);
    }

    [EzIPC]
    public Sender? GetPlayerInfo(ulong CID)
    {
        return P.DataProvider.GetPlayerInfo(CID);
    }
}
