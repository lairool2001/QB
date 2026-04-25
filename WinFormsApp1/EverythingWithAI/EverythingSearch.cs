using System.Runtime.InteropServices;
using System.Text;

namespace EverythingWithAI;

public static class EverythingSearch
{
    private const uint EVERYTHING_ERROR_IPC = 2;
    private const uint MAX_PATH = 32768;

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern bool Everything_SetSearchW(string lpSearchString);

    [DllImport("Everything64.dll")]
    private static extern bool Everything_QueryW(bool bWait);

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern uint Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetLastError();

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMax(uint dwMax);

    [DllImport("Everything64.dll")]
    private static extern void Everything_Reset();

    /// <summary>
    /// 執行 Everything 搜尋，回傳檔案路徑清單。
    /// </summary>
    public static List<string> Search(string query, uint maxResults = 2000)
    {
        var results = new List<string>();

        Everything_Reset();
        Everything_SetSearchW(query);
        Everything_SetMax(maxResults);

        if (!Everything_QueryW(true))
        {
            uint error = Everything_GetLastError();
            if (error == EVERYTHING_ERROR_IPC)
                throw new InvalidOperationException("Everything 未執行，請先啟動 Everything 搜尋程式。");
            throw new InvalidOperationException($"Everything 查詢失敗，錯誤碼：{error}");
        }

        uint count = Everything_GetNumResults();
        var sb = new StringBuilder((int)MAX_PATH);

        for (uint i = 0; i < count; i++)
        {
            sb.Clear();
            Everything_GetResultFullPathNameW(i, sb, MAX_PATH);
            results.Add(sb.ToString());
        }

        return results;
    }
}
