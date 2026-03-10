using CashFlow.Gui;

namespace CashFlow.Services;
public static class ServiceManager
{
    public static ThreadPool ThreadPool;
    public static MainWindow MainWindow;
    public static MemoryManager MemoryManager;
    public static CommandManager CommandManager;
    public static TradeDetectionManager TradeDetectionManager;
    public static EventWatcher EventWatcher;
    public static WorkerThread WorkerThread;
    public static TradeOverlay TradeOverlay;
    public static CashflowFileDialogManager CashflowFileDialogManager;
    public static IpcProvider IpcProvider;
}
