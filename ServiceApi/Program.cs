using System.Diagnostics;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();
builder.WebHost.UseUrls("http://10.35.10.79:5050");

// <-- เพิ่ม: ลงทะเบียน CORS
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var apiKey = builder.Configuration["ApiKey"]
             ?? throw new Exception("ตั้งค่า ApiKey ใน appsettings.json ก่อน");

var app = builder.Build();

// <-- เพิ่ม: ต้องอยู่ก่อน middleware ตรวจ API key
app.UseCors();

// ตรวจ API key
app.Use(async (ctx, next) =>
{
    // <-- เพิ่ม: ปล่อย preflight (OPTIONS) ผ่าน
    if (HttpMethods.IsOptions(ctx.Request.Method))
    {
        await next();
        return;
    }

    if (ctx.Request.Headers["X-Api-Key"] != apiKey)
    {
        ctx.Response.StatusCode = 401;
        return;
    }
    await next();
});

// 1) get all service
app.MapGet("/services", () =>
    Respond(RunSc("query", "state=", "all"), r => ParseServices(r.Output)));

// 2) start
app.MapPost("/services/{name}/start", (string name) =>
    !ValidName(name) ? BadName() : Respond(RunSc("start", name)));

// 3) stop
app.MapPost("/services/{name}/stop", (string name) =>
    !ValidName(name) ? BadName() : Respond(RunSc("stop", name)));

// 4) delete
app.MapDelete("/services/{name}", (string name) =>
    !ValidName(name) ? BadName() : Respond(RunSc("delete", name)));

// 5) create
app.MapPost("/services", (CreateRequest req) =>
{
    if (!ValidName(req.Name)) return BadName();
    if (!File.Exists(req.Path)) return Results.BadRequest(new { error = "ไม่พบไฟล์ exe" });

    return Respond(RunSc("create", req.Name, "binPath=", req.Path, "start=", "auto"));
});

app.Run();

// ---------- Helpers ----------
static bool ValidName(string s) => Regex.IsMatch(s ?? "", @"^[\w.\-]{1,256}$");
static IResult BadName() => Results.BadRequest(new { error = "ชื่อ service ไม่ถูกต้อง" });

static IResult Respond(ScResult r, Func<ScResult, object?>? map = null) => r.ExitCode switch
{
    0    => Results.Ok(map is null ? new { success = true, output = r.Output.Trim() } : map(r)),
    1060 => Results.NotFound(new { error = "ไม่พบ service นี้" }),
    5    => Results.Json(new { error = "Access denied: ต้องรันด้วยสิทธิ์ Administrator" }, statusCode: 403),
    1056 or 1062 => Results.Conflict(new { error = r.Output.Trim() }),
    _    => Results.Json(new { error = r.Output.Trim(), exitCode = r.ExitCode }, statusCode: 500)
};

static ScResult RunSc(params string[] args)
{
    var psi = new ProcessStartInfo("sc.exe")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (var a in args) psi.ArgumentList.Add(a);

    using var p = Process.Start(psi)!;
    string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
    p.WaitForExit();
    return new ScResult(p.ExitCode, output);
}

static List<Dictionary<string, object>> ParseServices(string output)
{
    var list = new List<Dictionary<string, object>>();
    Dictionary<string, object>? cur = null;

    foreach (var raw in output.Split('\n'))
    {
        var line = raw.Trim();
        int i = line.IndexOf(':');
        if (i < 0) continue;

        string key = line[..i].Trim(), val = line[(i + 1)..].Trim();
        if (key == "SERVICE_NAME") { cur = new(); list.Add(cur); }
        if (cur is null) continue;

        var words = key.ToLower().Split('_');
        string camel = words[0] + string.Concat(words.Skip(1).Select(w => char.ToUpper(w[0]) + w[1..]));

        var parts = val.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        cur[camel] = parts.Length == 2 && int.TryParse(parts[0], out int code)
            ? new { code, name = parts[1].Trim('(', ')') }
            : val;
    }
    return list;
}

record ScResult(int ExitCode, string Output);
record CreateRequest(string Name, string Path);