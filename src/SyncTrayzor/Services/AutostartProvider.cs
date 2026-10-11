using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;

namespace SyncTrayzor.Services
{
    public interface IAutostartProvider
    {
        bool IsEnabled { get; set; }
        bool CanRead { get; }
        bool CanWrite { get; }

        AutostartConfiguration GetCurrentSetup();
        void SetAutoStart(AutostartConfiguration config);
    }

    public class AutostartConfiguration
    {
        public bool AutoStart { get; set; }
        public bool StartMinimized { get; set; }

        public override string ToString()
        {
            return $"<AutostartConfiguration AutoStart={AutoStart} StartMinimized={StartMinimized}>";
        }
    }

    public class AutostartProvider : IAutostartProvider
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string applicationName = "irongoo";
        private const string runPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string runPathWithHive = @"HKEY_CURRENT_USER\" + runPath;
        // Matches 'irongoo' and 'irongoo (n)' (where n is a digit)
        private static readonly Regex keyRegex = new("^" + applicationName + @"(?: \((\d+)\))?$");
        private readonly string keyName;

        public bool IsEnabled { get; set; }

        private bool _canRead;
        public bool CanRead => IsEnabled && _canRead;

        private bool _canWrite;
        public bool CanWrite => IsEnabled && _canWrite;

        public AutostartProvider()
        {

            // Default
            IsEnabled = true;

            // Find a key, if we can, which points to our current location
            keyName = FindKeyNameAndCheckAccess();
        }

        private string FindKeyNameAndCheckAccess()
        {
            string keyName;

            try
            {
                keyName = FindKeyName();
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                // Can't even get the key name? 
                logger.Warn("Unable to find registry key name: do not have read access to the registry");
                return null;
            }

            try
            {
                using var key = OpenRegistryKey(true);
                if (key != null) // It's null if "there was an error"
                {
                    // We can open it, but not have access to edit this value
                    var value = key.GetValue(keyName);
                    if (value != null)
                    {
                        key.SetValue(keyName, value);
                    }
                    else
                    {
                        key.SetValue(keyName, string.Empty);
                        key.DeleteValue(keyName);
                    }

                    _canWrite = true;
                    _canRead = true;
                    logger.Debug("Have read/write access to the registry");
                    return keyName;
                }
            }
            catch (SecurityException) { }
            catch (UnauthorizedAccessException) { }

            try
            {
                using var key = OpenRegistryKey(false);
                if (key != null) // It's null if "there was an error"
                {
                    // We can open it, but not have access to read this value
                    var value = key.GetValue(keyName);

                    _canRead = true;
                    logger.Warn("Have read-only access to the registry");
                    return keyName;
                }
            }
            catch (SecurityException) { }
            catch (UnauthorizedAccessException) { }

            logger.Warn("Could find registry key name, but have no access to the registry");
            return null;
        }

        private string FindKeyName()
        {
            var numbersSeen = new List<int>();
            string foundKey = null;

            using (var key = OpenRegistryKey(false))
            {
                foreach (var entry in key.GetValueNames())
                {
                    var match = keyRegex.Match(entry);
                    if (match.Success)
                    {
                        // Keep a record of the highest number seen, in case we need to create a new one
                        var numberValue = match.Groups[1].Value;
                        if (numberValue == String.Empty)
                            numbersSeen.Add(1);
                        else
                            numbersSeen.Add(Int32.Parse(numberValue));

                        // See if this one points to our application
                        if (key.GetValue(entry) is string keyValue && keyValue.StartsWith($"\"{Environment.ProcessPath!}\""))
                        {
                            foundKey = entry;
                            break;
                        }
                    }
                }
            }

            // If we've seen a key that points to our application, then that's an easy win
            // If not, find the first gap in the list of key names, and use that to create our key
            if (foundKey != null)
                return foundKey;

            // No numbers seen? "irongoo". The logic below can't handle an empty list either
            if (numbersSeen.Count == 0)
                return applicationName;

            numbersSeen.Sort();
            var firstGap = Enumerable.Range(1, numbersSeen.Count).Except(numbersSeen).FirstOrDefault();
            // Value of 0 = no gaps
            var numberToUse = firstGap == 0 ? numbersSeen[numbersSeen.Count - 1] + 1 : firstGap;

            if (numberToUse == 1)
                return applicationName;
            else
                return $"{applicationName} ({numberToUse})";
        }

        private RegistryKey OpenRegistryKey(bool writable)
        {
            var key = Registry.CurrentUser.CreateSubKey(runPath, writable ? RegistryKeyPermissionCheck.ReadWriteSubTree : RegistryKeyPermissionCheck.ReadSubTree);
            return key;
        }

        public AutostartConfiguration GetCurrentSetup()
        {
            if (!CanRead)
                throw new InvalidOperationException("Don't have permission to read the registry");

            bool autoStart = false;
            bool startMinimized = false;

            using (var registryKey = OpenRegistryKey(false))
            {
                if (registryKey.GetValue(keyName) is string value)
                {
                    autoStart = true;
                    if (value.Contains(" -minimized"))
                        startMinimized = true;
                }
            }

            var config = new AutostartConfiguration() { AutoStart = autoStart, StartMinimized = startMinimized };
            logger.Debug("GetCurrentSetup determined that the current configuration is: {0}", config);
            return config;
        }

        public void SetAutoStart(AutostartConfiguration config)
        {
            if (!CanWrite)
                throw new InvalidOperationException("Don't have permission to write to the registry");

            logger.Debug("Setting AutoStart to {0}", config);

            using var registryKey = OpenRegistryKey(true);
            var keyExists = registryKey.GetValue(keyName) != null;

            if (config.AutoStart)
            {
                var path = $"\"{Environment.ProcessPath!}\"{(config.StartMinimized ? " -minimized" : "")}";
                logger.Debug("Autostart path: {0}", path);
                registryKey.SetValue(keyName, path);
            }
            else if (keyExists)
            {
                logger.Debug("Removing pre-existing registry key");
                registryKey.DeleteValue(keyName);
            }
        }
    }
}
