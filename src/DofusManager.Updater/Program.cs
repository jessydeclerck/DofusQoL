using System.Diagnostics;
using System.IO.Compression;

namespace DofusManager.Updater;

internal class Program
{
    private static StreamWriter? _logWriter;

    static async Task<int> Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: DofusManager.Updater <pid> <zipPath> <installDir>");
            return 1;
        }

        if (!int.TryParse(args[0], out var pid))
        {
            Console.WriteLine($"PID invalide : {args[0]}");
            return 1;
        }

        var zipPath = args[1];
        var installDir = args[2];

        var logPath = Path.Combine(installDir, "update.log");
        _logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };

        try
        {
            LogInfo($"Updater démarré — PID cible={pid}, zip={zipPath}, dir={installDir}");

            // Étape 1 : attendre la fermeture de l'application (max 30 secondes)
            LogInfo("Attente de la fermeture de l'application...");
            if (!await WaitForProcessExitAsync(pid, TimeSpan.FromSeconds(30)))
            {
                LogError("L'application n'a pas quitté dans les 30 secondes");
                return 1;
            }
            LogInfo("Application fermée");

            // Petit délai pour libérer les handles fichier
            await Task.Delay(500);

            // Étape 2 : backup de l'exe principal
            var mainExe = Path.Combine(installDir, "DofusManager.UI.exe");
            var backupExe = mainExe + ".bak";
            if (File.Exists(mainExe))
            {
                LogInfo($"Backup de {mainExe} vers {backupExe}");
                if (File.Exists(backupExe))
                    File.Delete(backupExe);
                File.Move(mainExe, backupExe);
            }

            // Étape 3 : renommer l'updater (Windows permet le rename d'un exe en cours)
            var updaterExe = Path.Combine(installDir, "DofusManager.Updater.exe");
            var updaterOld = updaterExe + ".old";
            if (File.Exists(updaterExe))
            {
                if (File.Exists(updaterOld))
                    File.Delete(updaterOld);
                File.Move(updaterExe, updaterOld);
                LogInfo("Updater renommé en .old");
            }

            // Étape 4 : extraire le zip en écrasant les fichiers existants
            LogInfo($"Extraction de {zipPath} vers {installDir}");
            ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);
            LogInfo("Extraction terminée");

            // Étape 5 : nettoyage
            if (File.Exists(backupExe))
            {
                try { File.Delete(backupExe); }
                catch { /* sera nettoyé la prochaine fois */ }
            }

            try { File.Delete(updaterOld); }
            catch { /* sera nettoyé la prochaine fois */ }

            try { File.Delete(zipPath); }
            catch { /* ignore */ }

            // Étape 6 : lancer la nouvelle version
            LogInfo("Lancement de la nouvelle version...");
            if (File.Exists(mainExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = mainExe,
                    WorkingDirectory = installDir,
                    UseShellExecute = true
                });
            }
            else
            {
                LogError($"Exécutable introuvable après extraction : {mainExe}");
                if (File.Exists(backupExe))
                {
                    File.Move(backupExe, mainExe);
                    LogInfo("Backup restauré");
                }
                return 1;
            }

            LogInfo("Mise à jour terminée avec succès");
            return 0;
        }
        catch (Exception ex)
        {
            LogError($"Erreur fatale : {ex}");

            // Tenter de restaurer le backup
            var mainExe = Path.Combine(installDir, "DofusManager.UI.exe");
            var backupExe = mainExe + ".bak";
            if (File.Exists(backupExe) && !File.Exists(mainExe))
            {
                try
                {
                    File.Move(backupExe, mainExe);
                    LogInfo("Backup restauré après erreur");
                }
                catch { /* échec critique */ }
            }

            return 1;
        }
        finally
        {
            _logWriter?.Dispose();
        }
    }

    private static async Task<bool> WaitForProcessExitAsync(int pid, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            using var cts = new CancellationTokenSource(timeout);
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (ArgumentException)
        {
            // Process déjà terminé
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static void LogInfo(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO  {message}";
        Console.WriteLine(line);
        _logWriter?.WriteLine(line);
    }

    private static void LogError(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR {message}";
        Console.Error.WriteLine(line);
        _logWriter?.WriteLine(line);
    }
}
