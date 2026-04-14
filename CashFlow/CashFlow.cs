using CashFlow.Data;
using CashFlow.Data.SqlDescriptors;
using CashFlow.DataProvider;
using CashFlow.DataProvider.Sqlite;
using Dalamud.Interface.ImGuiNotification;
using ECommons.Configuration;
using ECommons.Events;
using ECommons.EzSharedDataManager;
using ECommons.Funding;
using ECommons.GameHelpers;
using ECommons.Singletons;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using NightmareUI.Censoring;

namespace CashFlow;

public sealed unsafe class CashFlow : IDalamudPlugin
{
    public static CashFlow P;
    private Configuration Configuration;
    public DataProviderBase DataProvider;
    public static Configuration C => P.Configuration;

    // Track the last time we recorded gil
    private long LastPeriodicSave = 0;
    private readonly Dictionary<ulong, long> LastKnownTotalGilByCid = [];
    private const int InstantSpendCheckIntervalMs = 250;

    public CashFlow(IDalamudPluginInterface dalamudPluginInterface)
    {
        P = this;
        ECommonsMain.Init(dalamudPluginInterface, this);
        //PatreonBanner.IsOfficialPlugin = () => true;
        Configuration = EzConfig.Init<Configuration>();
        Censor.Config = Configuration.CensorConfig;
        DataProvider = new SqliteDataProvider();
        SingletonServiceManager.Initialize(typeof(ServiceManager));

        new TickScheduler(() =>
        {
            ProperOnLogin.RegisterAvailable(OnLogin, true);
        });

        Svc.Framework.Update += Framework_Update;
    }

    private void Framework_Update(Dalamud.Plugin.Services.IFramework framework)
    {
        if(!Player.Available || Player.CID == 0) return;

        // 1. Handle Retainer Gil Caching (Standard plugin behavior for Summoning Bell)
        if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
        {
            if (RetainerManager.Instance()->IsReady && EzThrottler.Throttle("SaveRetainerGil"))
            {
                C.CachedRetainerGil[Player.CID] = Utils.GetCurrentPlayerRetainerGil();
            }
        }

        // 2. Lightweight instant spend detection (separate from periodic logging)
        if(EzThrottler.Throttle("CashFlow.InstantSpendDetection", InstantSpendCheckIntervalMs))
        {
            var currentTotal = InventoryManager.Instance()->GetInventoryItemCount(1) + Utils.GetCurrentOrCachedPlayerRetainerGil();
            DetectAndNotifySpend(Player.CID, currentTotal);
        }

        // 2. Flexible heartbeat record for timeline graphing
        var intervalSeconds = Math.Clamp(C.GilRecordIntervalSeconds, 5, 3600);
        var intervalMs = intervalSeconds * 1000L;
        if (Player.Available && DateTimeOffset.Now.ToUnixTimeMilliseconds() - LastPeriodicSave > intervalMs)
        {
            RecordCurrentGil();
            LastPeriodicSave = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
    }

    private void RecordCurrentGil()
    {
        if (Player.CID == 0) return;

        var currentTotal = InventoryManager.Instance()->GetInventoryItemCount(1) + Utils.GetCurrentOrCachedPlayerRetainerGil();
        // Keep baseline in sync for log snapshots without producing duplicate alerts.
        LastKnownTotalGilByCid[Player.CID] = currentTotal;

        var entry = new GilRecordSqlDescriptor()
        {
            CidUlong = Player.CID,
            GilPlayer = InventoryManager.Instance()->GetInventoryItemCount(1),
            GilRetainer = Utils.GetCurrentOrCachedPlayerRetainerGil(),
            UnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
        };
        DataProvider.RecordPlayerGil(entry);
    }

    private void DetectAndNotifySpend(ulong cid, long currentTotal)
    {
        if(C.EnableSpendGilAlert && LastKnownTotalGilByCid.TryGetValue(cid, out var previousTotal) && currentTotal < previousTotal)
        {
            var spent = previousTotal - currentTotal;
            var content = C.SpendGilAlertText;
            if(C.ShowSpendAmountInAlert)
            {
                content += $" (-{spent:N0} gil)";
            }

            if(!string.IsNullOrWhiteSpace(C.SpendGilAlertImagePath))
            {
                content += $"\nImage: {C.SpendGilAlertImagePath}";
            }

            Svc.NotificationManager.AddNotification(new Notification
            {
                Content = content,
                Type = NotificationType.Warning,
                Minimized = false,
            });
            S.SpendGilOverlayManager.Trigger(spent);
        }

        LastKnownTotalGilByCid[cid] = currentTotal;
    }

    private void OnLogin()
    {
        DataProvider.RecordPlayerCID(Player.Object);
        RecordCurrentGil();
        LastPeriodicSave = DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public void Dispose()
    {
        // Removed ConditionChange unsubscription since we no longer use it
        Svc.Framework.Update -= Framework_Update;
        ECommonsMain.Dispose();
        P = null;
    }
}