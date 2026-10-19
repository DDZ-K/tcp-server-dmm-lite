using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace TcpServerDmmLite;

internal sealed class CommandServer
{
    readonly MesService _mes;
    readonly Dictionary<string, MethodInfo> _methods;
    TcpListener _listener;
    volatile bool _live;
    public Action<string> Log = _ => { };

    public CommandServer(MesService mes)
    {
        _mes = mes;
        _methods = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
        AddMethods(typeof(MesService), BindingFlags.Instance | BindingFlags.Public);
        AddMethods(typeof(IoCommands), BindingFlags.Static | BindingFlags.Public);
    }

    void AddMethods(Type type, BindingFlags flags)
    {
        foreach (var method in type.GetMethods(flags | BindingFlags.DeclaredOnly))
        {
            if (!method.Name.StartsWith("Spe_", StringComparison.Ordinal))
                continue;
            if (method.ReturnType != typeof(string))
                continue;
            var ps = method.GetParameters();
            if (ps.Length > 1 || (ps.Length == 1 && ps[0].ParameterType != typeof(string)))
                continue;
            _methods[method.Name] = method;
        }
    }

    public IReadOnlyCollection<string> Commands => _methods.Keys;

    public void Start(int port)
    {
        _live = true;
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Log("TCP 已监听 " + port);
        var thread = new Thread(Listen) { IsBackground = true, Name = "tcp-listen" };
        thread.Start();
    }

    public void Stop()
    {
        _live = false;
        try { _listener?.Stop(); } catch { /* 退出 */ }
    }

    void Listen()
    {
        while (_live)
        {
            TcpClient client;
            try { client = _listener.AcceptTcpClient(); }
            catch { break; }
            var worker = new Thread(() => Handle(client)) { IsBackground = true };
            worker.Start();
        }
    }

    void Handle(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            stream.ReadTimeout = 10 * 1000;
            while (_live)
            {
                var buffer = new byte[client.ReceiveBufferSize];
                int n;
                try { n = stream.Read(buffer, 0, buffer.Length); }
                catch
                {
                    Log("客户端读超时，断开");
                    break;
                }
                if (n <= 0)
                    break;

                var text = Encoding.ASCII.GetString(buffer, 0, n).Trim('\0', ' ', '\r', '\n');
                Log("收到 " + text);
                var reply = Dispatch(text);
                var bytes = Encoding.ASCII.GetBytes(reply);
                stream.Write(bytes, 0, bytes.Length);
                Log("回复 " + reply);
                Thread.Sleep(100);
            }
        }
    }

    public string Dispatch(string text)
    {
        try
        {
            var parts = (text ?? "").Split(new[] { '|' }, 2);
            var name = parts[0].Trim();
            var para = parts.Length > 1 ? parts[1].Trim(' ', '|') : "";
            if (!_methods.TryGetValue(name, out var method))
            {
                Log("没有这个命令: " + name);
                return "-888";
            }

            _mes.paraInput = MesService.StrToStrArray(para, '|');
            object target = method.IsStatic ? null : _mes;
            object[] args = method.GetParameters().Length == 0 ? null : new object[] { para };
            var ret = method.Invoke(target, args)?.ToString() ?? "";
            Log(name + " = " + ret);
            var wire = ToWire(ret);
            if (wire == "-888")
            {
                var err = IoCommands.LastError;
                if (string.IsNullOrEmpty(err))
                    err = MesService.sError;
                if (!string.IsNullOrEmpty(err))
                    Log("Error: " + err);
            }
            return wire;
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            Log("命令异常: " + inner.Message);
            return "-888";
        }
    }

    /// <summary>和旧 RunFuncByDict 一样：成功只回第二段，失败固定 -888。</summary>
    public static string ToWire(string funcReturn)
    {
        var retValues = (funcReturn ?? "").Split('|');
        if (retValues.Length == 0 || retValues[0] != "1")
            return "-888";
        return retValues.Length > 1 ? retValues[1] : "1";
    }
}

internal static class AppLog
{
    public static void Write(string message)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Log_TXT");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd") + ".txt");
            File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
        }
        catch
        {
            /* 日志失败不影响通讯 */
        }
    }
}
