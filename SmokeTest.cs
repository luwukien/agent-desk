using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace AgentDesk.Spike;

// Integration checks use the real WPF view + ConPTY + PowerShell.
// Input is injected via the connection API, NOT via a physical keyboard.
internal static class SmokeTest
{
    private static readonly List<string> Results = new();
    private static string FileAt(string name) => Path.Combine(Environment.CurrentDirectory, name);

    public static async Task RunAsync(MainWindow window)
    {
        int exitCode = 0;
        try
        {
            var a = window.Selected!;
            await a.Ready.WaitAsync(TimeSpan.FromSeconds(15));
            await WaitAsync(() => a.RecentOutput.Contains("PS "));
            await CommandFile(a, "$agentDeskMarker = 'alpha'; Set-Content a.txt ($PID.ToString() + '|' + $agentDeskMarker)", "a.txt");
            int pidA = a.ProcessId!.Value;
            Require(File.ReadAllText(FileAt("a.txt")).Trim() == $"{pidA}|alpha", "PowerShell input/output + PID");
            Require(a.RecentOutput.Length > 0, "Output received from ConPTY");

            var b = window.AddSession();
            await b.Ready.WaitAsync(TimeSpan.FromSeconds(15));
            await WaitAsync(() => b.RecentOutput.Contains("PS "));
            await CommandFile(b, "$agentDeskMarker = 'beta'; Set-Content b.txt ($PID.ToString() + '|' + $agentDeskMarker)", "b.txt");
            Require(a.ProcessId != b.ProcessId, "Two independent shell processes");
            window.SelectSession(a);
            a.SendText("Start-Sleep -Seconds 1; Set-Content hidden.txt 'continued'\r");
            window.SelectSession(b);
            await WaitAsync(() => File.Exists(FileAt("hidden.txt")));
            Require(File.ReadAllText(FileAt("hidden.txt")).Trim() == "continued", "Hidden tab keeps executing");
            for (int i = 0; i < 10; i++)
            {
                window.SelectSession(a);
                await Task.Delay(40);
                window.SelectSession(b);
                await Task.Delay(40);
            }
            window.SelectSession(a);
            await CommandFile(a, "Set-Content a-after.txt ($PID.ToString() + '|' + $agentDeskMarker)", "a-after.txt");
            Require(File.ReadAllText(FileAt("a-after.txt")).Trim() == $"{pidA}|alpha", "20 switches preserve PID and variables");

            int oldColumns = a.View.Columns;
            window.Width = 800;
            window.Height = 510;
            await Task.Delay(700);
            await CommandFile(a, "Set-Content size.txt ($Host.UI.RawUI.WindowSize.Width.ToString() + 'x' + $Host.UI.RawUI.WindowSize.Height)", "size.txt");
            var size = File.ReadAllText(FileAt("size.txt")).Trim();
            Require(a.View.Columns < oldColumns && size == $"{a.View.Columns}x{a.View.Rows}", "WPF resize reaches shell: " + size);

            a.SendText("Set-Content busy.txt 'started'; Start-Sleep -Seconds 60; Set-Content should-not-exist.txt 'bad'\r");
            await WaitAsync(() => File.Exists(FileAt("busy.txt")));
            a.SendText("\x03");
            await Task.Delay(700);
            await CommandFile(a, "Set-Content interrupted.txt 'ready'", "interrupted.txt");
            Require(!File.Exists(FileAt("should-not-exist.txt")), "Ctrl+C byte interrupts PowerShell command");

            await CommandFile(a,
                "$child = Start-Process powershell.exe -ArgumentList '-NoLogo -NoProfile -Command Start-Sleep -Seconds 120' -WindowStyle Hidden -PassThru; Set-Content child.txt $child.Id", "child.txt");
            int childId = int.Parse(File.ReadAllText(FileAt("child.txt")).Trim());
            Require(Alive(childId), "Child process started for close test");
            await window.RemoveSessionAsync(a);
            await WaitAsync(() => !Alive(pidA) && !Alive(childId));
            Require(!Alive(pidA) && !Alive(childId) && b.IsRunning, "Closing A kills shell + child; B survives");
            await CommandFile(b, "Set-Content b-after.txt ($PID.ToString() + '|' + $agentDeskMarker)", "b-after.txt");
            Require(File.ReadAllText(FileAt("b-after.txt")).Trim() == $"{b.ProcessId}|beta", "B remains interactive after closing A");

            var early = window.AddSession();
            await window.RemoveSessionAsync(early);
            Require(early.ProcessId == null || !Alive(early.ProcessId.Value), "Close immediately after opening");

            var bad = window.AddSession("agentdesk-nonexistent-shell-123.exe");
            try { await bad.Ready.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (Exception) { /* A startup error is expected. */ }
            Require(bad.Status.StartsWith("Lỗi:"), "Invalid executable reports startup failure");
            await window.RemoveSessionAsync(bad);

            var natural = window.AddSession();
            await natural.Ready.WaitAsync(TimeSpan.FromSeconds(15));
            await WaitAsync(() => natural.RecentOutput.Contains("PS "));
            natural.SendText("exit\r");
            await WaitAsync(() => natural.Status == "Đã thoát");
            await window.RemoveSessionAsync(natural);
            Results.Add("PASS: Shell exit command completes and tab can close");

            // This only checks that the installed CLI starts. It is not an agent task test.
            window.SelectSession(b);
            await CommandFile(b, "codex --version | Set-Content codex-version.txt", "codex-version.txt");
            string version = File.ReadAllText(FileAt("codex-version.txt")).Trim();
            Require(version.Contains("codex-cli"), "Installed CLI version via terminal: " + version);
            if (Environment.GetCommandLineArgs().Contains("--codex-probe"))
            {
                b.SendText("codex --no-daemon\r");
                await Task.Delay(6000);
                File.WriteAllText(FileAt("codex-probe.txt"), b.RecentOutput);
                Results.Add("UNVERIFIED: Agent prompt/response. Probe intentionally sends no input after CLI startup.");
                Results.Add("OBSERVATION: Physical keys, navigation, approval, and interruption of a running agent task require manual checks.");
            }
        }
        catch (Exception ex)
        {
            exitCode = 1;
            Results.Add("FAIL: " + ex);
        }
        finally
        {
            try { await window.FinishSmokeAsync(); }
            catch (Exception ex) { exitCode = 1; Results.Add("CLEANUP FAIL: " + ex); }
            File.WriteAllText(FileAt("smoke-results.json"), JsonSerializer.Serialize(new
            {
                ExecutedAt = DateTimeOffset.Now, Windows = Environment.OSVersion.ToString(),
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                ExitCode = exitCode, Results
            }, new JsonSerializerOptions { WriteIndented = true }));
            Application.Current.Shutdown(exitCode);
        }
    }

    private static async Task CommandFile(TerminalSession session, string command, string file)
    {
        session.SendText(command + "\r");
        await WaitAsync(() => File.Exists(FileAt(file)) && new FileInfo(FileAt(file)).Length > 0);
        await Task.Delay(150);
    }

    private static bool Alive(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Results.Add("PASS: " + message);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Timed out waiting for shell evidence.");
            await Task.Delay(100);
        }
    }
}
