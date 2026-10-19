using System.Runtime.InteropServices;

namespace TcpServerDmmLite;

/// <summary>
/// 官方 N1616.h：stdcall。DO_Bit 顺序是 (句柄, 通道 0-15, 0关/1开)。
/// 旧工程 DllImport 把参数名写成反的，但 Spe_N1616_Output 的实参顺序和头文件一致。
/// </summary>
internal static class N1616Card
{
    const string Dll = "N1616.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CC1616_OpenDevice")]
    static extern IntPtr OpenDevice(int devnum);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CC1616_CloseDevice")]
    static extern int CloseDevice(IntPtr hDevice);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CC1616_DI")]
    public static extern int ReadIn(IntPtr hDevice);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CC1616_DI_Bit")]
    public static extern int ReadInBit(IntPtr hDevice, int channel);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CC1616_DO")]
    public static extern int WriteOut(IntPtr hDevice, int data);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CC1616_DO_Bit")]
    public static extern int WriteOutBit(IntPtr hDevice, int channel, int bitData);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CC1616_RDO")]
    public static extern int ReadOut(IntPtr hDevice);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CC1616_RDO_Bit")]
    public static extern int ReadOutBit(IntPtr hDevice, int channel);

    public static IntPtr Handle { get; private set; } = new IntPtr(-1);
    public static bool IsOpen { get; private set; }
    public static string LastError { get; private set; } = "";
    public static int OpenIndex { get; private set; }

    public static bool EnsureOpen(int index)
    {
        if (IsOpen && Handle != IntPtr.Zero && Handle != new IntPtr(-1))
            return true;

        try
        {
            Handle = OpenDevice(index);
        }
        catch (Exception ex)
        {
            IsOpen = false;
            LastError = "加载 N1616.dll 失败: " + ex.Message;
            return false;
        }

        if (Handle == IntPtr.Zero || Handle == new IntPtr(-1))
        {
            IsOpen = false;
            LastError = "打开 N1616 失败（板卡号 " + index + "）。检查驱动、CH367DLL.dll 和 VC++2010 x86。";
            return false;
        }

        OpenIndex = index;
        IsOpen = true;
        LastError = "";
        return true;
    }

    public static void Close()
    {
        if (!IsOpen)
            return;
        try { CloseDevice(Handle); } catch { /* 退出时驱动已卸载 */ }
        IsOpen = false;
        Handle = new IntPtr(-1);
    }
}

internal static class IoCommands
{
    public static int CardIndex;
    public static string LastError = "";

    public static string Spe_N1616_Output(string para)
    {
        try
        {
            if (!N1616Card.EnsureOpen(CardIndex))
            {
                LastError = N1616Card.LastError;
                return "0|0";
            }

            var sArr = Split(para);
            int code;
            if (sArr.Length > 1)
            {
                int channel = int.Parse(sArr[0].Trim()) - 1;
                int bit = int.Parse(sArr[1].Trim());
                if (channel < 0 || channel > 15 || (bit != 0 && bit != 1))
                {
                    LastError = "通道或电平超出 1-16 / 0-1";
                    return "0|-1";
                }
                code = N1616Card.WriteOutBit(N1616Card.Handle, channel, bit);
            }
            else
            {
                var word = sArr[0].Trim();
                int data = word.Length <= 6
                    ? Convert.ToInt32(word, 16)
                    : Convert.ToInt32(word, 2);
                code = N1616Card.WriteOut(N1616Card.Handle, data);
            }

            if (code != 0)
            {
                LastError = "N1616 输出失败，返回 " + code;
                return "0|-1";
            }

            LastError = "";
            return "1|1";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return "0|0";
        }
    }

    public static string Spe_N1616_Input(string para)
    {
        try
        {
            if (!N1616Card.EnsureOpen(CardIndex))
            {
                LastError = N1616Card.LastError;
                return "0|0";
            }

            var sArr = Split(para);
            if (sArr.Length == 0 || sArr[0].Length == 0)
            {
                int word = N1616Card.ReadIn(N1616Card.Handle) & 0xFFFF;
                return "1|" + word.ToString();
            }

            int channel = int.Parse(sArr[0].Trim()) - 1;
            if (channel < 0 || channel > 15)
            {
                LastError = "输入通道超出 1-16";
                return "0|-1";
            }

            int bit = N1616Card.ReadInBit(N1616Card.Handle, channel);
            if (bit < 0)
            {
                LastError = "读输入失败";
                return "0|-1";
            }

            return "1|" + bit.ToString();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return "0|0";
        }
    }

    static string[] Split(string para) =>
        (para ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
}
