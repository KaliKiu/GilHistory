using CashFlow.Data.SqlDescriptors;
using Dalamud.Bindings.ImPlot;
using NightmareUI.Censoring;

namespace CashFlow.Gui.BaseTabs;

public unsafe class TabGilHistory : BaseTab<GilRecordSqlDescriptor>
{
    private ulong SelectedCID = 0;
    private bool AutoFitRequested = true;
    private readonly Dictionary<ulong, long> PrevGil = [];
    private readonly List<ulong> RecordedCIDs = [];
    private readonly Dictionary<ulong, float[]> ChartXByCid = [];
    private readonly Dictionary<ulong, float[]> ChartYByCid = [];

    public override bool ShouldDrawPaginator => false;

    public override List<GilRecordSqlDescriptor> SortData(List<GilRecordSqlDescriptor> data)
    {
        return SortColumn switch
        {
            0 => Order(data, x => S.MainWindow.CIDMap.SafeSelect(x.CidUlong).ToString()),
            1 => Order(data, x => x.GilPlayer + x.GilRetainer),
            _ => data
        };
    }

    public override void DrawTable()
    {
        ImGuiEx.Text($"Entries loaded: {Data.Count:N0}");
        ImGui.SameLine();
        if(ImGui.Button("Auto-fit graph"))
        {
            AutoFitRequested = true;
        }
        DrawTimelinePlot();

        DrawPaginator();
        if(ImGuiEx.BeginDefaultTable(["Your Character", "~Total Gil", "Diff", "Timestamp"], extraFlags: ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate))
        {
            ImGuiCheckSorting();
            for(var i = IndexBegin; i < IndexEnd; i++)
            {
                var t = Data[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.Text(S.MainWindow.CIDMap.TryGetValue(t.CidUlong, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{t.CidUlong:X16}"));
                ImGui.TableNextColumn();
                ImGuiEx.Text($"{t.GilPlayer + t.GilRetainer:N0}");
                ImGui.TableNextColumn();
                Utils.DrawColoredGilText(t.Diff);
                ImGui.TableNextColumn();
                ImGuiEx.Text(DateTimeOffset.FromUnixTimeMilliseconds(t.UnixTime).ToPreferredTimeString());
            }
            ImGui.EndTable();
        }
    }

    private void DrawTimelinePlot()
    {
        if(ChartXByCid.Count == 0) return;
        if(!ImPlot.BeginPlot("##GilHistoryTimeline", new Vector2(-1, 300))) return;

        ImPlot.SetupAxis(ImAxis.X1, "Minutes from first sample");
        ImPlot.SetupAxis(ImAxis.Y1, "Total gil (millions)");
        ImPlot.SetupAxisFormat(ImAxis.Y1, "%gM");
        if(AutoFitRequested)
        {
            ImPlot.SetNextAxesToFit();
            AutoFitRequested = false;
        }
        ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, 3f);

        foreach(var cid in ChartXByCid.Keys.OrderBy(x => x))
        {
            if(SelectedCID != 0 && cid != SelectedCID) continue;
            var x = ChartXByCid[cid];
            var y = ChartYByCid[cid];
            if(x.Length == 0) continue;

            var label = S.MainWindow.CIDMap.TryGetValue(cid, out var sender)
                ? Censor.Character(sender.ToString())
                : Censor.Hide($"{cid:X16}");
            ImPlot.PlotLine(label, ref x[0], ref y[0], x.Length);
            ImPlot.PlotScatter($"##pts_{cid}", ref x[0], ref y[0], x.Length);
        }

        ImPlot.EndPlot();
    }

    public override void DrawSearchBar(out bool updateBlocked)
    {
        updateBlocked = false;
        var isOpen = false;
        var changed = false;

        if(S.WorkerThread.IsBusy) return;
        ImGuiEx.InputWithRightButtonsArea(() =>
        {
            var names = new Dictionary<ulong, string> { [0] = "All" };
            foreach(var entry in S.MainWindow.CIDMap.Where(x => RecordedCIDs.Contains(x.Key)))
            {
                names[entry.Key] = entry.Value.ToString();
            }

            if(ImGuiEx.Combo<ulong>("##selectPlayer", ref SelectedCID, [0, .. RecordedCIDs], names: names))
            {
                changed = true;
            }
        }, () => DrawDateFilter(out isOpen));

        if(changed || isOpen)
        {
            Data.Clear();
            NeedsUpdate = true;
            AutoFitRequested = true;
            updateBlocked = true;
        }
    }

    public override bool ShouldAddData(GilRecordSqlDescriptor data)
    {
        if(C.Blacklist.Contains(data.CidUlong)) return false;
        if(C.DisplayExclusionsGilHistory.Contains(data.CidUlong)) return false;
        if(!RecordedCIDs.Contains(data.CidUlong)) RecordedCIDs.Add(data.CidUlong);
        if(SelectedCID != 0) return data.CidUlong == SelectedCID;
        return true;
    }

    public override List<GilRecordSqlDescriptor> LoadData()
    {
        PrevGil.Clear();
        RecordedCIDs.Clear();
        ChartXByCid.Clear();
        ChartYByCid.Clear();
        AutoFitRequested = true;
        return P.DataProvider.GetGilTimelineRecords();
    }

    public override bool ProcessSearchByItem(GilRecordSqlDescriptor x) => true;

    public override bool ProcessSearchByName(GilRecordSqlDescriptor x)
    {
        var name = S.MainWindow.CIDMap.SafeSelect(x.CidUlong);
        return name.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase);
    }

    public override void AddData(GilRecordSqlDescriptor data, List<GilRecordSqlDescriptor> list)
    {
        base.AddData(data, list);
        var cur = data.GilPlayer + data.GilRetainer;
        data.Diff = PrevGil.TryGetValue(data.CidUlong, out var value) ? cur - value : 0;
        PrevGil[data.CidUlong] = cur;
    }

    public override void OnPostLoadDataAsync(List<GilRecordSqlDescriptor> newData)
    {
        foreach(var group in newData.GroupBy(x => x.CidUlong))
        {
            var ordered = group.OrderBy(x => x.UnixTime).ToList();
            if(ordered.Count == 0) continue;

            var minUnix = ordered[0].UnixTime;
            ChartXByCid[group.Key] = [.. ordered.Select(x => (float)((x.UnixTime - minUnix) / 60000.0))];
            ChartYByCid[group.Key] = [.. ordered.Select(x => (float)((x.GilPlayer + x.GilRetainer) / 1_000_000.0))];
        }
    }
}
