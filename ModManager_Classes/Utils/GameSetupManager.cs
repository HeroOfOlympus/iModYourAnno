﻿using Imya.Models;
using Imya.Models.NotifyPropertyChanged;
using System.Diagnostics;

using System.Timers;


namespace Imya.Utils
{
    /// <summary>
    /// Validates and setups the basics of a modded game installation.
    /// 
    /// - Mods folder
    /// - Modloader
    /// - 
    /// 
    /// </summary>
    /// 

    public enum ModloaderInstallationState { Installed, Uninstalled, Deactivated }

    public class GameSetupManager : PropertyChangedNotifier
    {
        public static readonly String ProfilesDirectoryName = "profiles";

        public static GameSetupManager Instance { get; } = new GameSetupManager();

        private InstallationValidator Validator;

        //public ModloaderInstallation ModLoader { get; private set; }

        #region EVENTS
        public delegate void GameRootPathChangedEventHandler(String newPath);
        public event GameRootPathChangedEventHandler GameRootPathChanged = delegate { };

        public delegate void ModDirectoryNameChangedEventHandler(String newName);
        public event ModDirectoryNameChangedEventHandler ModDirectoryNameChanged = delegate { };

        public delegate void GameStartEventHandler();
        public event GameStartEventHandler GameStarted = delegate { };

        public delegate void GameCloseEventHandler(int GameExitCode, bool IsRegularExit = true);
        public event GameCloseEventHandler GameClosed = delegate { };

        #endregion

        // File System Watchers
#pragma warning disable IDE0052 // Never used, but we want to keep them until GameSetupManager dies
        private FileSystemWatcher? ModDirectoryWatcher;
        // private FileSystemWatcher ModLoaderWatcher; // This is TODO
#pragma warning restore IDE0052

        public bool IsGameRunning
        {
            get => _isGameRunning;
            private set
            {
                _isGameRunning = value;
                OnPropertyChanged(nameof(IsGameRunning));
            }
        }
        private bool _isGameRunning;

        public String GameRootPath { get => _gameRootPath;
            private set
            {
                _gameRootPath = value;
                OnPropertyChanged(nameof(GameRootPath));
            }
        }
        private String _gameRootPath;

        public String? ExecutablePath { get; private protected set; }
        public String? ExecutableDir { get; private protected set; }

        public String ModDirectoryName {
            get => _modDirectoryName;
            private set
            {
                _modDirectoryName = value;
                OnPropertyChanged(nameof(ModDirectoryName));
            }
        }
        private String _modDirectoryName;

        public bool IsValidSetup
        {
            get { return _isValid; }
            private set { _isValid = value; OnPropertyChanged(nameof(IsValidSetup)); }
        }
        private bool _isValid;

        public bool ModloaderActivationDesiredOnStart 
        {
            get => _modloaderActivationDesiredOnStart;
            set
            {
                _modloaderActivationDesiredOnStart = value;
                OnPropertyChanged(nameof(ModloaderActivationDesiredOnStart));
            }
        }
        private bool _modloaderActivationDesiredOnStart; 

        public bool IsModloaderInstalled => ModloaderState == ModloaderInstallationState.Installed;

        public ModloaderInstallationState ModloaderState 
        {
            get => _modloaderState;
            set
            {
                _modloaderState = value;
                OnPropertyChanged(nameof(ModloaderState));
            }
        }
        private ModloaderInstallationState _modloaderState = ModloaderInstallationState.Uninstalled;

        public String DownloadDirectory
        {
            get => _downloadDirectory;
            private set {
                _downloadDirectory = value;
                OnPropertyChanged(nameof(DownloadDirectory));
            }
        }
        private String _downloadDirectory = String.Empty;

        public GameSetupManager()
        {
            GameStarted += () => IsGameRunning = true;
            GameClosed += (x, y) => IsGameRunning = false;
        }

        #region DIRECTORY_RELATED

        public String GetModDirectory() => Path.Combine(GameRootPath, ModDirectoryName);

        public String GetProfilesDirectory() => Path.Combine(GameRootPath, ProfilesDirectoryName);

        /// <summary>
        /// Set download directory. Relative to executable.
        /// </summary>
        public void SetDownloadDirectory(string downloadDirectory) => DownloadDirectory = downloadDirectory;

        public void SetGamePath(String gamePath, bool autoSearchIfInvalid = false)
        {
            String executablePath = Path.Combine(gamePath, "Bin", "Win64", "Anno1800.exe");
            if (!File.Exists(executablePath) && autoSearchIfInvalid)
            {
                var foundGamePath = GameScanner.GetInstallDirFromRegistry() ?? "";
                executablePath = Path.Combine(foundGamePath, "Bin", "Win64", "Anno1800.exe");
                if (File.Exists(executablePath))
                    gamePath = foundGamePath; // only replace if found, otherwise keep "wrong" path in the settings.
            }

            GameRootPath = gamePath;
            Validator = new InstallationValidator(GameRootPath);

            if (File.Exists(executablePath))
            {
                ExecutablePath = executablePath;
                ExecutableDir = Path.Combine(gamePath, "Bin", "Win64");
                IsValidSetup = true;
            }
            else
            {
                ExecutablePath = null;
                ExecutableDir = null;
                IsValidSetup = false;
            }
            
            GameRootPathChanged(gamePath);
            CreateWatchers();
            UpdateModloaderInstallStatus();
        }

        public void SetModDirectoryName(String ModDirectoryName)
        { 
            this.ModDirectoryName = ModDirectoryName;
            ModDirectoryNameChanged(ModDirectoryName);

            Console.WriteLine($"Changed Mod Directory Name to {ModDirectoryName}");
        }

        #endregion

        private void CreateWatchers() => ModDirectoryWatcher = CreateWatcher(Path.Combine(GameRootPath));

        public void UpdateModloaderInstallStatus() => ModloaderState = Validator.CheckInstallation();

        public void EnsureModloaderActivation(bool desired)
        {
            UpdateModloaderInstallStatus();

            if (desired && ModloaderState == ModloaderInstallationState.Deactivated)
            {
                ActivateModloader(); 
                return;
            }
            if (!desired && ModloaderState == ModloaderInstallationState.Installed) 
                DeactivateModloader();
        }

        private void ActivateModloader()
        {
            if (ModloaderState != ModloaderInstallationState.Deactivated) return;
            try
            {
                File.Move(Path.Combine(ExecutableDir, "python35.dll"), Path.Combine(ExecutableDir, "python35_ubi.dll"));
                File.Move(Path.Combine(ExecutableDir, "modloader.dll"), Path.Combine(ExecutableDir, "python35.dll"));

                ModloaderState = ModloaderInstallationState.Installed;
            }
            catch (Exception e)
            {
                Console.WriteLine("Modloader Activation unsuccessful");
                return;
            }
        }

        private void DeactivateModloader()
        {
            if (!IsModloaderInstalled) return;

            try
            {
                //move python35 to modloader and python35_ubi to python35. modloader.dll is junk anyway, just override it.
                File.Move(Path.Combine(ExecutableDir, "python35.dll"), Path.Combine(ExecutableDir, "modloader.dll"), true);
                File.Move(Path.Combine(ExecutableDir, "python35_ubi.dll"), Path.Combine(ExecutableDir, "python35.dll"));

                ModloaderState = ModloaderInstallationState.Deactivated;
            }
            catch (Exception e)
            {
                Console.WriteLine("Modloader Deactivation unsuccessful");
                return;
            }            
        }

        private FileSystemWatcher? CreateWatcher(string pathToWatch)
        {
            if (!Directory.Exists(pathToWatch))
            {
                // There are many other things that can go wrong besides a non-existant path, but let's take this for now
                return null;
            }
                
            return new FileSystemWatcher
            {
                Path = pathToWatch,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
                Filter = "*"
            };
        }

        public void StartGame()
        {
            if (ExecutablePath == null)
                return;

            EnsureModloaderActivation(ModloaderActivationDesiredOnStart);

            _ = Task.Run(async () =>
            {
                using Process process = new Process();
                process.StartInfo.FileName = ExecutablePath;
                process.EnableRaisingEvents = true;
                process.Exited += OnGameLaunchComplete;
                process.Start();

                Console.WriteLine("Anno 1800 started.");

                GameStarted.Invoke();

                await process.WaitForExitAsync();
            }
            );
        }

        private void OnGameLaunchComplete(object sender, EventArgs e)
        {
            Console.WriteLine($"Start Process exited! Starting Game Scan");

            _ = Task.Run (
                async () =>
                {
                    Process? RunningGame = await ScanForRunningGameAsync(30);

                    if (RunningGame is null) return;

                    RunningGame.EnableRaisingEvents = true;
                    RunningGame.Exited += OnGameExit;

                    await RunningGame.WaitForExitAsync();
                } 
            );
        }

        private void OnGameExit(object sender, EventArgs e)
        {
            var process = sender as Process;
            Console.WriteLine($"Anno 1800 exited with Code {process?.ExitCode}");

            int exitCode = process?.ExitCode ?? -1;
            GameClosed.Invoke(exitCode);
        }

        /// <summary>
        /// Asynchronously searches for a running Anno 1800 process 
        /// </summary>
        /// <param name="TimeoutInSeconds"></param>
        /// <returns></returns>
        public async Task<Process?> ScanForRunningGameAsync(int TimeoutInSeconds)
        {
            Process? process = null;
            
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            for (int i = TimeoutInSeconds; 
                    process is null && 
                    await periodicTimer.WaitForNextTickAsync() 
                    && i > 0; i--  )
            {
                GameScanner.TryGetRunningGame(out process);
            }

            Console.WriteLine(process is not null ? "running process found!" : "failed to retrieve any running process");

            if (process is null)
            {
                GameClosed.Invoke(-1, false);
            }

            return process;
        }
    }
}
