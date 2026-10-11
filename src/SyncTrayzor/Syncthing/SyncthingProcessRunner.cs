using NLog;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SyncTrayzor.Syncthing;

public enum SyncthingExitStatus
{
    // From https://github.com/syncthing/syncthing/blob/master/cmd/syncthing/main.go#L67
    Success = 0,
    Error = 1,
    NoUpgradeAvailable = 2,
    Restarting = 3,
    Upgrading = 4
}

public class ProcessStoppedEventArgs : EventArgs
{
    public SyncthingExitStatus ExitStatus { get; }

    public ProcessStoppedEventArgs(SyncthingExitStatus exitStatus)
    {
        ExitStatus = exitStatus;
    }
}

public interface ISyncthingProcessRunner : IDisposable
{
    string ExecutablePath { get; set; }
    string ApiKey { get; set; }
    string HostAddress { get; set; }
    string CustomHomeDir { get; set; }
    List<string> CommandLineFlags { get; set; }
    IDictionary<string, string> EnvironmentalVariables { get; set; }
    bool DenyUpgrade { get; set; }
    SyncthingPriorityLevel SyncthingPriorityLevel { get; set; }
    bool HideDeviceIds { get; set; }

    event EventHandler Starting;
    event EventHandler ProcessRestarted;
    event EventHandler<MessageLoggedEventArgs> MessageLogged;
    event EventHandler<ProcessStoppedEventArgs> ProcessStopped;

    void Start();
    void Kill();
    void KillAllSyncthingProcesses();
    bool UpgradeSyncthing();
}

public class SyncthingProcessRunner : ISyncthingProcessRunner
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly Logger syncthingLogger = LogManager.GetLogger("Syncthing");

    private static readonly string[] defaultArguments = new[] { "--no-browser", "--no-restart" };

    // Leave just the first set of digits, removing everything after it
    private static readonly Regex deviceIdHideRegex =
        new(@"-[0-9A-Z]{7}-[0-9A-Z]{7}-[0-9A-Z]{7}-[0-9A-Z]{7}-[0-9A-Z]{7}-[0-9A-Z]{7}-[0-9A-Z]{7}");

    private static readonly Dictionary<SyncthingPriorityLevel, ProcessPriorityClass> priorityMapping = new()
    {
        { SyncthingPriorityLevel.AboveNormal, ProcessPriorityClass.AboveNormal },
        { SyncthingPriorityLevel.Normal, ProcessPriorityClass.Normal },
        { SyncthingPriorityLevel.BelowNormal, ProcessPriorityClass.BelowNormal },
        { SyncthingPriorityLevel.Idle, ProcessPriorityClass.Idle },
    };

    private readonly object processLock = new();
    private Process process;

    private const int numRestarts = 4;
    private const int systemShutdownExitStatus = 0x40010004;
    private static readonly TimeSpan restartThreshold = TimeSpan.FromMinutes(1);
    private readonly List<DateTime> starts = new();
    private bool isKilling;

    public string ExecutablePath { get; set; }
    public string ApiKey { get; set; }
    public string HostAddress { get; set; }
    public string CustomHomeDir { get; set; }
    public List<string> CommandLineFlags { get; set; } = new();
    public IDictionary<string, string> EnvironmentalVariables { get; set; } = new Dictionary<string, string>();
    public bool DenyUpgrade { get; set; }
    public SyncthingPriorityLevel SyncthingPriorityLevel { get; set; }
    public bool HideDeviceIds { get; set; }

    public event EventHandler Starting;
    public event EventHandler ProcessRestarted;
    public event EventHandler<MessageLoggedEventArgs> MessageLogged;
    public event EventHandler<ProcessStoppedEventArgs> ProcessStopped;

    public void Start()
    {
        logger.Debug("SyncthingProcessRunner.Start called");
        // This might cause our config to be set...
        OnStarting();

        StartInternal(isRestart: false);
    }

    private void StartInternal(bool isRestart)
    {
        logger.Info("Starting syncthing: {0}", ExecutablePath);

        isKilling = false;

        if (!File.Exists(ExecutablePath))
            throw new Exception($"Unable to find Syncthing at path {ExecutablePath}");

        // Overlay Syncthing GUI assets so external browsers also get irongoo branding
        if (!string.IsNullOrWhiteSpace(CustomHomeDir))
        {
            IrongooGuiBranding.ApplyToSyncthingHome(CustomHomeDir);
            // Decline usage-report dialog + dismiss GUI-auth banner before Syncthing boots
            IrongooGuiBranding.SuppressStartupPrompts(CustomHomeDir, ExecutablePath);
        }

        var processStartInfo = new ProcessStartInfo()
        {
            FileName = ExecutablePath,
            Arguments = String.Join(" ", GenerateArguments()),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            // In case people are using relative folder paths, mirror the shortcut
            WorkingDirectory = Path.GetDirectoryName(typeof(SyncthingProcessRunner).Assembly.Location),
        };

        processStartInfo.EnvironmentVariables["STGUIAPIKEY"] = ApiKey;

        if (DenyUpgrade)
            processStartInfo.EnvironmentVariables["STNOUPGRADE"] = "1";
        if (isRestart)
            processStartInfo.EnvironmentVariables["STRESTART"] = "yes";

        foreach (var kvp in EnvironmentalVariables)
        {
            processStartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        lock (processLock)
        {
            KillInternal();

            if (starts.Count >= numRestarts)
                starts.RemoveRange(0, starts.Count - numRestarts + 1);
            starts.Add(DateTime.UtcNow);

            process = Process.Start(processStartInfo);

            try
            {
                process.PriorityClass = priorityMapping[SyncthingPriorityLevel];
            }
            catch (InvalidOperationException e)
            {
                // This can happen if syncthing.exe stops really really quickly (see #150)
                // We shouldn't crash out: instead, keep going and see what the exit code was
                logger.Warn("Failed to set process priority", e);
            }

            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (o, e) => DataReceived(e.Data);
            process.ErrorDataReceived += (o, e) => DataReceived(e.Data);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.Exited += (o, e) => OnProcessExited();
        }
    }

    public void Kill()
    {
        logger.Info("Killing Syncthing process");
        lock (processLock)
        {
            KillInternal();
        }
    }

    // MUST BE CALLED FROM WITHIN A LOCK!
    private void KillInternal()
    {
        if (process != null)
        {
            try
            {
                process.Kill();
                process = null;
            }
            // These can happen in rare cases, and we don't care. See the docs for Process.Kill
            catch (Win32Exception e)
            {
                logger.Warn("KillInternal failed with an error", e);
            }
            catch (InvalidOperationException e)
            {
                logger.Warn("KillInternal failed with an error", e);
            }
        }
    }

    private IEnumerable<string> GenerateArguments()
    {
        var args = new List<string>(defaultArguments)
        {
            $"--gui-address=\"{HostAddress}\""
        };

        if (!String.IsNullOrWhiteSpace(CustomHomeDir))
            args.Add($"--home=\"{CustomHomeDir}\"");

        args.AddRange(CommandLineFlags);

        return args;
    }

    private void DataReceived(string data)
    {
        if (!String.IsNullOrWhiteSpace(data))
        {
            if (HideDeviceIds)
                data = deviceIdHideRegex.Replace(data, "");
            OnMessageLogged(data);
        }
    }

    public void Dispose()
    {
        lock (processLock)
        {
            KillInternal();
        }
    }

    private void OnProcessExited()
    {
        SyncthingExitStatus exitStatus;
        lock (processLock)
        {
            exitStatus = process == null ? SyncthingExitStatus.Success : (SyncthingExitStatus)process.ExitCode;
            process = null;
        }

        logger.Debug("Syncthing process stopped with exit status {0}", exitStatus);
        if (exitStatus == SyncthingExitStatus.Restarting || exitStatus == SyncthingExitStatus.Upgrading)
        {
            logger.Debug("Syncthing process requested restart, so restarting");
            OnProcessRestarted();
            Start();
        }
        else if (exitStatus != SyncthingExitStatus.Success && (int)exitStatus != systemShutdownExitStatus &&
                 !isKilling)
        {
            if (starts.Count >= numRestarts && DateTime.UtcNow - starts[0] < restartThreshold)
            {
                logger.Warn("{0} restarts in less than {1}: not restarting again", numRestarts, restartThreshold);
                OnProcessStopped(exitStatus);
                starts.Clear();
            }
            else
            {
                logger.Info("Syncthing exited. Restarting...");
                StartInternal(isRestart: true);
            }
        }
        else
        {
            OnProcessStopped(exitStatus);
        }
    }

    private void OnStarting()
    {
        Starting?.Invoke(this, EventArgs.Empty);
    }

    private void OnProcessStopped(SyncthingExitStatus exitStatus)
    {
        ProcessStopped?.Invoke(this, new ProcessStoppedEventArgs(exitStatus));
    }

    private void OnProcessRestarted()
    {
        ProcessRestarted?.Invoke(this, EventArgs.Empty);
    }

    private void OnMessageLogged(string logMessage)
    {
        logger.Debug(logMessage);
        syncthingLogger.Info(logMessage);
        MessageLogged?.Invoke(this, new MessageLoggedEventArgs(logMessage));
    }

    public void KillAllSyncthingProcesses()
    {
        // So we don't restart ourselves...
        isKilling = true;

        logger.Debug("Kill all Syncthing processes");
        foreach (var process in Process.GetProcessesByName("syncthing"))
        {
            try
            {
                process.Kill();
            }
            catch (Exception e)
            {
                logger.Warn(e, $"Failed to kill Syncthing process ${process}");
            }
        }
    }

    public bool UpgradeSyncthing()
    {
        try
        {
            logger.Info("Attempting to upgrade syncthing: {0}", ExecutablePath);

            if (!File.Exists(ExecutablePath))
            {
                logger.Warn("Cannot upgrade syncthing: Binary does not exist");
                return false;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = "upgrade",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(typeof(SyncthingProcessRunner).Assembly.Location),
            };

            processStartInfo.EnvironmentVariables["STGUIAPIKEY"] = ApiKey;

            foreach (var kvp in EnvironmentalVariables)
            {
                processStartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            var upgradeProcess = Process.Start(processStartInfo)!;
            upgradeProcess.OutputDataReceived += (o, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) logger.Info(e.Data);
            };
            upgradeProcess.ErrorDataReceived += (o, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) logger.Warn(e.Data);
            };
            upgradeProcess.BeginOutputReadLine();
            upgradeProcess.BeginErrorReadLine();
            upgradeProcess.WaitForExit();
            var exitCode = upgradeProcess.ExitCode;
            upgradeProcess.Close();
            return exitCode is (int)SyncthingExitStatus.Success or (int)SyncthingExitStatus.Upgrading
                or (int)SyncthingExitStatus.Restarting or (int)SyncthingExitStatus.NoUpgradeAvailable;
        }
        catch (Exception e)
        {
            logger.Warn(e, "Failed to upgrade syncthing");
            return false;
        }
    }
}