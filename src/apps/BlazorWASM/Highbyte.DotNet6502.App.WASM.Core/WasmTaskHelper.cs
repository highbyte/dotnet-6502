using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Highbyte.DotNet6502.App.WASM;

public static class WasmTaskHelper
{
    public static void Observe(Task task, string operationName)
    {
        _ = task.ContinueWith(
            static (completedTask, state) =>
            {
                if (completedTask.Exception is { } exception)
                {
                    Debug.WriteLine($"[{state}] {exception.GetBaseException()}");
                }
            },
            operationName,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
