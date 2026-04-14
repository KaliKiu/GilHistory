using CashFlow.Gui;

namespace CashFlow.Services;
public static class ServiceManager
{
    public static ThreadPool ThreadPool;
    public static MainWindow MainWindow;
    public static CommandManager CommandManager;
    public static WorkerThread WorkerThread;
    public static CashflowFileDialogManager CashflowFileDialogManager;
    public static IpcProvider IpcProvider;
    public static SpendGilOverlayManager SpendGilOverlayManager;
}
