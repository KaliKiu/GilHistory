using CashFlow.Data;
using CashFlow.Data.ExplicitStructs;
using CashFlow.Data.SqlDescriptors;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;

namespace CashFlow.Services;
public unsafe class EventWatcher : IDisposable
{
    public ShopPurchaseSqlDescriptor LastClickedItem = null;
    public MerchantInfo LastMerchantInfo = null;

    private EventWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "ItemSearchResult", OnItemSearchResultSetup);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "ItemSearchResult", OnItemSearchResultFinalize);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "MerchantShop", OnMerchantShopSetup);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "MerchantShop", OnMerchantShopFinalize);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "MerchantShop", OnMerchantShopUpdate);
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreSetup, "ItemSearchResult", OnItemSearchResultSetup);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "ItemSearchResult", OnItemSearchResultFinalize);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "MerchantShop", OnMerchantShopSetup);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "MerchantShop", OnMerchantShopFinalize);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "MerchantShop", OnMerchantShopUpdate);
    }

    private void OnMerchantShopSetup(AddonEvent type, AddonArgs args)
    {
        S.MemoryManager.ProcessEventLogMessageHook.Enable();
        LastMerchantInfo = null;
    }

    private void OnMerchantShopFinalize(AddonEvent type, AddonArgs args)
    {
        //PluginLog.Information($"Finalize");
    }

    private void OnMerchantShopUpdate(AddonEvent type, AddonArgs args)
    {
        var data = AgentMerchantSettingInfo.Instance();
        if(data != null)
        {
            LastMerchantInfo = new(*data, "");
            if(Svc.Targets.Target is ICharacter c && c.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventNpc && Svc.Data.GetExcelSheet<ENpcBase>().TryGetRow(c.DataId, out var result) && result.ENpcData[0].RowId == 721407)
            {
                LastMerchantInfo.Name = c.Name.ToString();
            }
        }
    }

    private void OnItemSearchResultSetup(AddonEvent type, AddonArgs args)
    {
        LastClickedItem = null;
        S.MemoryManager.FireCallbackHook.Enable();
    }

    private void OnItemSearchResultFinalize(AddonEvent type, AddonArgs args)
    {
        LastClickedItem = null;
        S.MemoryManager.FireCallbackHook.Pause();
    }
}
