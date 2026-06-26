using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NetPulse.App.Core;

namespace NetPulse.App.Infrastructure
{
    public class ConfigChangedEventArgs : EventArgs
    {
        public AppConfig Config { get; }
        public ConfigChangedEventArgs(AppConfig config) => Config = config;
    }

    public class AppConfig
    {
        public List<Target> Targets { get; set; } = new();
        public int IntervalMs { get; set; } = 1000;
        public int LogRetentionDays { get; set; } = 7;
        public string ExportPath { get; set; } = "Exports";
        public string HmacKey { get; set; } = string.Empty;
        public bool UseUtcTimestamps { get; set; } = true;
    }

    public class ConfigManager : IDisposable
    {
        private readonly string _configFilePath;
        private readonly FileSystemWatcher _watcher;
        private AppConfig _config = new();
        private readonly object _lock = new();
        private DateTime _lastRead = DateTime.MinValue;

        public event EventHandler<ConfigChangedEventArgs>? OnConfigChanged;

        public AppConfig Config
        {
            get
            {
                lock (_lock)
                {
                    return _config;
                }
            }
        }

        public ConfigManager(string? configFilePath = null)
        {
            _configFilePath = configFilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            LoadConfig();

            string? directory = Path.GetDirectoryName(_configFilePath);
            string filename = Path.GetFileName(_configFilePath);

            _watcher = new FileSystemWatcher
            {
                Path = string.IsNullOrEmpty(directory) ? AppDomain.CurrentDomain.BaseDirectory : directory,
                Filter = filename,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            _watcher.Changed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            lock (_lock)
            {
                var lastWriteTime = File.GetLastWriteTime(_configFilePath);
                if (lastWriteTime - _lastRead < TimeSpan.FromMilliseconds(500))
                {
                    return;
                }
                _lastRead = lastWriteTime;
            }

            // Small delay to allow the saving process to release file lock
            System.Threading.Thread.Sleep(100);

            LoadConfig();
        }

        public void LoadConfig()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_configFilePath))
                    {
                        string json = File.ReadAllText(_configFilePath);
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var loaded = JsonSerializer.Deserialize<AppConfig>(json, options);
                        if (loaded != null)
                        {
                            _config = loaded;
                            OnConfigChanged?.Invoke(this, new ConfigChangedEventArgs(_config));
                        }
                    }
                    else
                    {
                        // Save defaults if not exist
                        _config = new AppConfig
                        {
                            Targets = new List<Target>
                            {
                                new Target { Address = "192.168.1.1", DisplayName = "Yerel Ağ Geçidi" },
                                new Target { Address = "8.8.8.8", DisplayName = "Dış DNS (Google)" }
                            },
                            IntervalMs = 1000,
                            LogRetentionDays = 7,
                            ExportPath = "Exports",
                            HmacKey = "",
                            UseUtcTimestamps = true
                        };
                        SaveConfig();
                    }
                }
                catch (Exception)
                {
                    // Fallback to defaults or keep last configuration
                }
            }
        }

        public void SaveConfig()
        {
            lock (_lock)
            {
                try
                {
                    string directory = Path.GetDirectoryName(_configFilePath) ?? string.Empty;
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(_config, options);

                    _watcher.EnableRaisingEvents = false;
                    File.WriteAllText(_configFilePath, json);
                    _watcher.EnableRaisingEvents = true;
                }
                catch (Exception)
                {
                    // Suppress or log
                }
            }
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }
    }
}
