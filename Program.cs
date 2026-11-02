using System.Diagnostics;
using System.Text.Json;

if (args.Length == 0) { ShowHelp(); return; }

string command = args[0].ToLower();

try
{
    switch (command)
    {
        case "create":
            if (args.Length < 3) throw new Exception("Usage: create <name> <exe-path>");
            Console.WriteLine(RunSc("create", args[1], $"binPath= \"{args[2]}\"", "start= auto"));
            break;
        case "start":  Console.WriteLine(RunSc("start", args[1])); break;
        case "stop":   Console.WriteLine(RunSc("stop", args[1])); break;
        case "delete": Console.WriteLine(RunSc("delete", args[1])); break;

        case "status":
            PrintJson(ParseServices(RunSc("query", args[1])).FirstOrDefault());
            break;

        case "list":
            PrintJson(ParseServices(RunSc("query", "state=", "all")));
            break;

        default: ShowHelp(); break;
    }
}
catch (Exception ex)
{
    PrintJson(new { error = ex.Message });
}

static void PrintJson(object? value) =>
    Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }));

static List<Dictionary<string, object>> ParseServices(string output)
{
    var services = new List<Dictionary<string, object>>();
    Dictionary<string, object>? current = null;

    foreach (var raw in output.Split('\n'))
    {
        var line = raw.Trim();
        int idx = line.IndexOf(':');
        if (idx < 0) continue;

        string key = line[..idx].Trim();
        string value = line[(idx + 1)..].Trim();

        if (key == "SERVICE_NAME")
        {
            current = new Dictionary<string, object>();
            services.Add(current);
        }
        if (current is null) continue;

        string name = ToCamel(key);

        // รูปแบบ "1  STOPPED" หรือ "0  (0x0)" → { code, name }
        var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out int code))
            current[name] = new { code, name = parts[1].Trim('(', ')') };
        else
            current[name] = value;
    }
    return services;
}

static string ToCamel(string key)
{
    var words = key.ToLower().Split('_');
    return words[0] + string.Concat(words.Skip(1).Select(w => char.ToUpper(w[0]) + w[1..]));
}

static string RunSc(params string[] arguments)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);

    process.Start();
    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new Exception($"sc.exe exited with code {process.ExitCode}: {output}{error}".Trim());

    return output;
}

static void ShowHelp() => Console.WriteLine("""
    ServiceManager Commands:

    create <name> <exe-path>
    start  <name>
    stop   <name>
    delete <name>
    status <name>   (JSON)
    list            (JSON)
    """);