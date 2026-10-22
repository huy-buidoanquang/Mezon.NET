// Real-SFU e2e harness for Mezon.Net.Sdk.Sfu (port of the Rust SDK's `cargo xtask e2e`).
//
//   dotnet run --project tools/SfuE2e -- up [--mode dynamic|fixed2|alone|decoupled] [--net host|bridge] [--no-build]
//   dotnet run --project tools/SfuE2e -- down [--mode <mode>] [--keep]
//   dotnet run --project tools/SfuE2e -- test [--image <sdk image>] [-- <dotnet test args>]
//
// `up` starts nats + mezon-sfu in Docker. `--net host` (default on Windows/macOS) publishes the SFU
// on 127.0.0.1 so tests on this machine can join; `--net bridge` (default on Linux) keeps it on a
// private network. The JWT secret is generated once into stack/.secrets/jwt (git-ignored) and never
// printed; connection details for tests go to stack/.runtime/<mode>.json. Ports and subnets differ
// from the other SDKs' stacks, so all can run at the same time. `SFU_IMAGE` runs an existing image
// and is never rebuilt. `test` runs the SFU test project in a .NET SDK container attached to
// every running bridge stack.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SfuE2e;

internal static class Program
{
    private sealed record Stack(string Subnet, int MediaPort, int SignalingPort, int NatsPort);

    private const string TestProject = "tests/Mezon.Net.Sdk.Sfu.Tests/Mezon.Net.Sdk.Sfu.Tests.csproj";

    private static Stack? StackFor(string mode) => mode switch
    {
        "dynamic" => new Stack("172.28.40", 7040, 8040, 4262),
        "fixed2" => new Stack("172.28.41", 7041, 8041, 4263),
        "alone" => new Stack("172.28.42", 7042, 8042, 4264),
        "decoupled" => new Stack("172.28.43", 7043, 8043, 4265),
        _ => null,
    };

    private static string Project(string mode) => $"sfu-e2e-dotnet-{mode}";

    private static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "up" => Up(args[1..]),
                "down" => Down(args[1..]),
                "test" => Test(args[1..]),
                _ => throw new HarnessException("usage: dotnet run --project tools/SfuE2e -- up|down|test [flags]"),
            };
        }
        catch (HarnessException e)
        {
            Console.Error.WriteLine($"sfu-e2e: {e.Message}");
            return 1;
        }
    }

    private sealed class HarnessException(string message) : Exception(message);

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Mezon.Net.sln")))
            {
                return dir.FullName;
            }
        }

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Mezon.Net.sln")))
            {
                return dir.FullName;
            }
        }

        throw new HarnessException("Mezon.Net.sln not found above the harness or the current directory");
    }

    private static string StackDir() => Path.Combine(RepoRoot(), "tools", "SfuE2e", "stack");

    private static string? Flag(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static bool Has(string[] args, string name) => args.Contains(name);

    private static Dictionary<string, string> EnvFor(string stackDir, string mode, string net)
    {
        var s = StackFor(mode) ?? throw new HarnessException($"unknown mode {mode}");
        var env = new Dictionary<string, string>
        {
            ["E2E_PROJECT"] = Project(mode),
            ["E2E_MODE"] = mode,
            ["E2E_SUBNET_PREFIX"] = s.Subnet,
            ["E2E_MEDIA_PORT"] = s.MediaPort.ToString(),
            ["E2E_SIGNALING_PORT"] = s.SignalingPort.ToString(),
            ["E2E_NATS_PORT"] = s.NatsPort.ToString(),
            ["E2E_SFU_DOCKERFILE"] = Path.Combine(stackDir, "sfu", "Dockerfile"),
            ["SFU_PUBLIC_HOST"] = net == "host" ? "127.0.0.1" : $"{s.Subnet}.10",
        };
        if (Environment.GetEnvironmentVariable("SFU_SRC") is not { Length: > 0 })
        {
            env["SFU_SRC"] = Path.GetFullPath(Path.Combine(RepoRoot(), "..", "mezon-sfu"));
        }

        return env;
    }

    private static List<string> Compose(string stackDir, string net)
    {
        var args = new List<string> { "compose", "-f", Path.Combine(stackDir, "compose.yaml") };
        if (net == "host")
        {
            args.Add("-f");
            args.Add(Path.Combine(stackDir, "compose.host.yaml"));
        }

        return args;
    }

    private static ProcessStartInfo Docker(IReadOnlyDictionary<string, string>? env, IEnumerable<string> args, bool capture = false)
    {
        var info = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        if (env is not null)
        {
            foreach (var (key, value) in env)
            {
                info.Environment[key] = value;
            }
        }

        return info;
    }

    private static int Run(ProcessStartInfo info)
    {
        using var process = Process.Start(info) ?? throw new HarnessException($"could not start {info.FileName}");
        process.WaitForExit();
        return process.ExitCode;
    }

    private static (int Code, string Output) Capture(ProcessStartInfo info)
    {
        using var process = Process.Start(info) ?? throw new HarnessException($"could not start {info.FileName}");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static void EnsureSecrets(string stackDir)
    {
        var secrets = Path.Combine(stackDir, ".secrets");
        Directory.CreateDirectory(secrets);
        var secret = Path.Combine(secrets, "jwt");
        if (!File.Exists(secret))
        {
            File.WriteAllText(secret, Convert.ToHexString(RandomNumberGenerator.GetBytes(48)).ToLowerInvariant() + "\n");
        }

        var env = Path.Combine(stackDir, ".env.e2e");
        if (!File.Exists(env))
        {
            File.WriteAllText(env, "# Extra SFU environment for the e2e stack (git-ignored).\nSFU_MULTI_SESSION_USER_IDS=\n");
        }
    }

    private static string CheckSfuSource(string stackDir, IReadOnlyDictionary<string, string> env)
    {
        var lockText = File.ReadAllText(Path.Combine(stackDir, "sfu.lock")).Trim();
        if (Environment.GetEnvironmentVariable("SFU_IMAGE") is { Length: > 0 })
        {
            return lockText;
        }

        var src = Environment.GetEnvironmentVariable("SFU_SRC") is { Length: > 0 } fromEnv
            ? Path.GetFullPath(fromEnv, stackDir)
            : env["SFU_SRC"];
        if (!File.Exists(Path.Combine(src, "Dockerfile")))
        {
            throw new HarnessException($"mezon-sfu source not found at {src} (set SFU_SRC or SFU_IMAGE)");
        }

        var git = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-C", src, "rev-parse", "HEAD" })
        {
            git.ArgumentList.Add(arg);
        }

        var head = "";
        try
        {
            head = Capture(git).Output.Trim().Split('\n')[0].Trim();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }

        if (head.Length == 40 && head != lockText)
        {
            var message = $"mezon-sfu is at {head[..12]} but stack/sfu.lock pins {lockText[..12]}";
            if (Environment.GetEnvironmentVariable("CI") is { Length: > 0 })
            {
                throw new HarnessException(message);
            }

            Console.Error.WriteLine($"sfu-e2e: warning: {message}");
        }

        return head.Length == 40 ? head : lockText;
    }

    private static int Up(string[] args)
    {
        var mode = Flag(args, "--mode") ?? "dynamic";
        var s = StackFor(mode) ?? throw new HarnessException($"unknown --mode {mode}");
        var net = Flag(args, "--net") ?? (OperatingSystem.IsLinux() ? "bridge" : "host");
        if (net is not ("host" or "bridge"))
        {
            throw new HarnessException($"unknown --net {net}");
        }

        if (Capture(Docker(null, ["info"], capture: true)).Code != 0)
        {
            throw new HarnessException("cannot reach the Docker daemon. Start Docker Desktop (or dockerd) and retry.");
        }

        var stackDir = StackDir();
        EnsureSecrets(stackDir);
        var env = EnvFor(stackDir, mode, net);
        var commit = CheckSfuSource(stackDir, env);
        var upArgs = Compose(stackDir, net);
        upArgs.AddRange(["up", "-d", "--wait", "--wait-timeout", "180"]);
        if (!Has(args, "--no-build") && Environment.GetEnvironmentVariable("SFU_IMAGE") is not { Length: > 0 })
        {
            upArgs.Add("--build");
        }

        Console.WriteLine($"sfu-e2e: starting {Project(mode)} ({net} network)...");
        var status = Run(Docker(env, upArgs));
        var logArgs = Compose(stackDir, net);
        logArgs.AddRange(["logs", "sfu"]);
        if (Capture(Docker(env, logArgs, capture: true)).Output.Contains("io_uring_queue_init_params failed", StringComparison.Ordinal))
        {
            throw new HarnessException("the SFU could not create its io_uring ring (seccomp). Check security_opt in stack/compose.yaml.");
        }

        if (status != 0)
        {
            throw new HarnessException($"docker compose up failed. Inspect with: dotnet run --project tools/SfuE2e -- down --mode {mode} --keep");
        }

        var (host, nats) = net == "host"
            ? ("127.0.0.1", $"nats://127.0.0.1:{s.NatsPort}")
            : ($"{s.Subnet}.10", $"nats://{s.Subnet}.5:4222");
        var runtime = new Dictionary<string, object>
        {
            ["mode"] = mode,
            ["net"] = net,
            ["project"] = Project(mode),
            ["sfuCommit"] = commit,
            ["wsUrl"] = $"ws://{host}:{s.SignalingPort}/ws",
            ["mediaPort"] = s.MediaPort,
            ["natsUrl"] = nats,
            ["jwtSecretFile"] = "tools/SfuE2e/stack/.secrets/jwt",
        };
        Directory.CreateDirectory(Path.Combine(stackDir, ".runtime"));
        File.WriteAllText(
            Path.Combine(stackDir, ".runtime", $"{mode}.json"),
            JsonSerializer.Serialize(runtime, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"sfu-e2e: {Project(mode)} is up at ws://{host}:{s.SignalingPort}/ws");
        return 0;
    }

    private static string? RuntimeNet(string stackDir, string mode)
    {
        var path = Path.Combine(stackDir, ".runtime", $"{mode}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.TryGetProperty("net", out var net) ? net.GetString() : null;
    }

    private static IEnumerable<string> RecordedModes(string stackDir)
    {
        var dir = Path.Combine(stackDir, ".runtime");
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.json").Select(f => Path.GetFileNameWithoutExtension(f)!).Order(StringComparer.Ordinal)
            : [];
    }

    private static int Down(string[] args)
    {
        var stackDir = StackDir();
        var modes = Flag(args, "--mode") is { } only ? [only] : RecordedModes(stackDir).ToList();
        if (modes.Count == 0)
        {
            Console.WriteLine("sfu-e2e: no running stacks recorded in tools/SfuE2e/stack/.runtime/");
            return 0;
        }

        var secretPath = Path.Combine(stackDir, ".secrets", "jwt");
        var secret = File.Exists(secretPath) ? File.ReadAllText(secretPath).Trim() : "";
        foreach (var mode in modes)
        {
            _ = StackFor(mode) ?? throw new HarnessException($"unknown mode {mode}");
            var net = RuntimeNet(stackDir, mode) ?? "bridge";
            var env = EnvFor(stackDir, mode, net);
            var logArgs = Compose(stackDir, net);
            logArgs.AddRange(["logs", "--no-color", "--timestamps"]);
            var logs = Capture(Docker(env, logArgs, capture: true)).Output;
            if (secret.Length > 0)
            {
                logs = logs.Replace(secret, "<redacted>", StringComparison.Ordinal);
            }

            var artifacts = Path.Combine(RepoRoot(), "tools", "SfuE2e", "artifacts");
            Directory.CreateDirectory(artifacts);
            var file = Path.Combine(artifacts, $"{Project(mode)}.log");
            File.WriteAllText(file, logs, new UTF8Encoding(false));
            Console.WriteLine($"sfu-e2e: logs saved to {file}");
            if (!Has(args, "--keep"))
            {
                var downArgs = Compose(stackDir, net);
                downArgs.AddRange(["down", "-v", "--remove-orphans"]);
                Run(Docker(env, downArgs));
                File.Delete(Path.Combine(stackDir, ".runtime", $"{mode}.json"));
            }
        }

        return 0;
    }

    private static int Test(string[] args)
    {
        var root = RepoRoot();
        var stackDir = StackDir();
        var image = Flag(args, "--image") ?? "mcr.microsoft.com/dotnet/sdk:10.0";
        var separator = Array.IndexOf(args, "--");
        var extra = separator >= 0 ? args[(separator + 1)..] : [];
        var networks = new List<string>();
        foreach (var mode in RecordedModes(stackDir))
        {
            if (RuntimeNet(stackDir, mode) == "bridge")
            {
                networks.Add($"{Project(mode)}_sfu");
            }
            else
            {
                Console.Error.WriteLine($"sfu-e2e: {Project(mode)} runs on the host network and is skipped in the container");
            }
        }

        const string name = "sfu-sdk-dotnet-test";
        Capture(Docker(null, ["rm", "-f", name], capture: true));
        var create = new List<string>
        {
            "create", "--name", name, "-w", "/src",
            "-e", "SFU_E2E_NET=bridge",
            "-e", "MEZON_NET_ROOT=/src",
            "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
            "-e", "DOTNET_NOLOGO=1",
            "-v", $"{root}:/src",
            "-v", "sdk-dotnet-nuget:/root/.nuget/packages",
            "-v", "sdk-dotnet-artifacts:/artifacts",
        };

        // The protocol tables and the canonical schema are checked against sibling checkouts when present.
        var spec = Path.GetFullPath(Path.Combine(root, "..", "mezon-sfu", "docs", "protocol"));
        if (File.Exists(Path.Combine(spec, "close-codes.json")))
        {
            create.AddRange(["-v", $"{spec}:/mezon-sfu-protocol:ro", "-e", "MEZON_SFU_PROTOCOL_DIR=/mezon-sfu-protocol"]);
        }

        var schema = Path.GetFullPath(Path.Combine(root, "..", "mezon-protocol", "api"));
        if (File.Exists(Path.Combine(schema, "api.proto")))
        {
            create.AddRange(["-v", $"{schema}:/mezon-protocol/api:ro", "-e", "MEZON_PROTOCOL_DIR=/mezon-protocol"]);
        }

        create.AddRange([image, "dotnet", "test", TestProject, "--artifacts-path", "/artifacts"]);
        create.AddRange(extra);
        if (Capture(Docker(null, create, capture: true)) is { Code: not 0 } failed)
        {
            throw new HarnessException($"docker create failed: {failed.Output.Trim()}");
        }

        try
        {
            foreach (var network in networks)
            {
                if (Run(Docker(null, ["network", "connect", network, name])) != 0)
                {
                    throw new HarnessException($"could not attach {network}");
                }
            }

            return Run(Docker(null, ["start", "-a", name])) == 0 ? 0 : 1;
        }
        finally
        {
            Capture(Docker(null, ["rm", "-f", name], capture: true));
        }
    }
}
