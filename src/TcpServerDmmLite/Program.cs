namespace TcpServerDmmLite;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        if (args != null && args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
            Environment.Exit(SelfTest());

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    static int SelfTest()
    {
        var mes = new MesService();
        StationConfig.Load().ApplyTo(mes);
        var server = new CommandServer(mes);
        var log = new StringWriter();
        server.Log = msg => log.WriteLine(msg);
        const int port = 60999;
        server.Start(port);
        Thread.Sleep(200);

        var direct = server.Dispatch("Spe_NotACommand|1");
        var wired = CommandServer.ToWire("1|abc|drop");
        var panelDisabled = server.Dispatch("Spe_PartProcessed_Multi|abc");
        string socketReply;
        using (var client = new System.Net.Sockets.TcpClient("127.0.0.1", port))
        using (var stream = client.GetStream())
        {
            var req = System.Text.Encoding.ASCII.GetBytes("Spe_NotACommand|1");
            stream.Write(req, 0, req.Length);
            var buf = new byte[32];
            int n = stream.Read(buf, 0, buf.Length);
            socketReply = System.Text.Encoding.ASCII.GetString(buf, 0, n);
        }

        server.Stop();
        Console.WriteLine("commands=" + server.Commands.Count);
        Console.WriteLine("direct=" + direct);
        Console.WriteLine("wired=" + wired);
        Console.WriteLine("panelDisabled=" + panelDisabled);
        Console.WriteLine("socket=" + socketReply);
        string[] removed = { "Spe_PartReceived_Multi", "Spe_PartProcessed_Multi", "Spe_CH367_Input", "Spe_CH367_Output" };
        var removedAbsent = removed.All(name => !server.Commands.Contains(name));
        Console.WriteLine("removedCommandsAbsent=" + removedAbsent);
        Console.WriteLine("registered=" + string.Join(",", server.Commands.OrderBy(name => name)));
        var ok = direct == "-888" && wired == "abc" && panelDisabled == "-888" && socketReply == "-888" && server.Commands.Contains("Spe_PartReceived") && removedAbsent && server.Commands.Contains("Spe_PartProcessed_MultiToSingle") && server.Commands.Contains("Spe_N1616_Output") && server.Commands.Contains("Spe_N1616_Input");
        Console.WriteLine(ok ? "SELFTEST_OK" : "SELFTEST_FAIL");
        if (!ok)
            Console.WriteLine(log.ToString());
        return ok ? 0 : 2;
    }
}
