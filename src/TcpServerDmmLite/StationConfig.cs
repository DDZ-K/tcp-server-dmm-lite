namespace TcpServerDmmLite;

internal sealed class StationConfig
{
    public string MesServer { get; set; } = "127.0.0.1";
    public int MesPort { get; set; } = 55365;
    public string TelegramPath { get; set; } = "Log_XML";
    public int ListenPort { get; set; } = 60000;
    public int CardIndex { get; set; }

    public static string IniPath => Path.Combine(AppContext.BaseDirectory, "configure", "Station.ini");

    public static StationConfig Load()
    {
        var cfg = new StationConfig();
        if (!File.Exists(IniPath))
            return cfg;

        var section = "";
        foreach (var raw in File.ReadAllLines(IniPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (section.Equals("MES", StringComparison.OrdinalIgnoreCase))
            {
                if (key.Equals("server", StringComparison.OrdinalIgnoreCase)) cfg.MesServer = value;
                else if (key.Equals("port", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var p)) cfg.MesPort = p;
                else if (key.Equals("telegramPath", StringComparison.OrdinalIgnoreCase)) cfg.TelegramPath = value;
            }
            else if (section.Equals("SERVER", StringComparison.OrdinalIgnoreCase) && key.Equals("port", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var lp))
                cfg.ListenPort = lp;
            else if (section.Equals("N1616", StringComparison.OrdinalIgnoreCase) && key.Equals("index", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var idx))
                cfg.CardIndex = idx;
        }

        return cfg;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(IniPath)!);
        File.WriteAllText(IniPath,
            "[MES]\r\n" +
            "telegramPath = " + TelegramPath + "\r\n" +
            "server = " + MesServer + "\r\n" +
            "port = " + MesPort + "\r\n\r\n" +
            "[SERVER]\r\n" +
            "port = " + ListenPort + "\r\n\r\n" +
            "[N1616]\r\n" +
            "index = " + CardIndex + "\r\n");
    }

    public void ApplyTo(MesService mes)
    {
        mes.dictMES["server"] = MesServer;
        mes.dictMES["port"] = MesPort.ToString();
        mes.dictMES["telegramPath"] = TelegramPath;
    }
}
