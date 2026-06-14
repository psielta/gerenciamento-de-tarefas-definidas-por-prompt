using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace Thoth.Desktop;

/// <summary>
/// Inicia e gerencia o processo da API (Thoth.Api.exe) na sessao do usuario.
/// Rodar a API como o proprio usuario (e nao como servico LocalSystem) garante que
/// terminais e CLIs filhos (codex/grok/claude) herdem a identidade e a home corretas,
/// encontrando as credenciais em C:\Users\&lt;usuario&gt;.
/// </summary>
internal sealed class ApiProcessHost : IDisposable
{
    private static readonly Uri HealthUri = new("http://localhost:8091/");
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    private readonly object gate = new();
    private Process? apiProcess;
    private SafeJobHandle? jobHandle;
    private bool startedByUs;

    /// <summary>
    /// Garante que a API esteja no ar em http://localhost:8091. Reaproveita uma instancia
    /// ja existente; caso contrario inicia Thoth.Api.exe como filho desta sessao.
    /// </summary>
    public async Task EnsureRunningAsync(CancellationToken cancellationToken)
    {
        if (await IsApiRespondingAsync(cancellationToken))
        {
            return;
        }

        lock (gate)
        {
            if (apiProcess is { HasExited: false })
            {
                return;
            }

            StartApiProcess();
        }
    }

    private void StartApiProcess()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "Thoth.Api.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"Thoth.Api.exe nao foi encontrado em {AppContext.BaseDirectory}.",
                exePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--environment Production",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Nao foi possivel iniciar Thoth.Api.exe.");

        apiProcess = process;
        startedByUs = true;
        AssignToKillOnCloseJob(process);
    }

    private void AssignToKillOnCloseJob(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var job = new SafeJobHandle(handle);

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
                },
            };

            var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            var infoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, infoPtr, false);
                if (!SetInformationJobObject(
                        handle,
                        JobObjectExtendedLimitInformation,
                        infoPtr,
                        (uint)length))
                {
                    job.Dispose();
                    return;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }

            if (!AssignProcessToJobObject(handle, process.Handle))
            {
                job.Dispose();
                return;
            }

            jobHandle = job;
        }
        catch
        {
            // O Job Object e apenas uma rede de seguranca; Stop() ainda encerra a API.
        }
    }

    private static async Task<bool> IsApiRespondingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, HealthUri);
            using var response = await Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            return (int)response.StatusCode < 500;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (startedByUs && apiProcess is { HasExited: false } process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Processo pode ter encerrado sozinho.
                }
            }

            apiProcess?.Dispose();
            apiProcess = null;

            jobHandle?.Dispose();
            jobHandle = null;
        }
    }

    public void Dispose() => Stop();

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        int jobObjectInformationClass,
        IntPtr lpJobObjectInformation,
        uint cbJobObjectInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private sealed class SafeJobHandle(IntPtr handle) : IDisposable
    {
        private IntPtr handle = handle;

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
