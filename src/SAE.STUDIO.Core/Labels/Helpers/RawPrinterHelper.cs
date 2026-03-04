using System.Collections;
using System.Runtime.InteropServices;

namespace SAE.STUDIO.Core.Labels.Helpers
{
    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public required string pDocName;
            [MarshalAs(UnmanagedType.LPStr)]
            public required string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)]
            public required string pDataType;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool SendBytesToPrinter(string printerName, byte[] bytes, string docName = "")
        {
            IntPtr pBytes = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, pBytes, bytes.Length);
            try
            {
                return SendBytesToPrinter(printerName, pBytes, bytes.Length, docName);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pBytes);
            }
        }

        public static bool SendBytesToPrinter(string printerName, IntPtr pBytes, int count, string docName = "")
        {
            if (OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
            {
                try
                {
                    var docInfo = new DOCINFOA { pDocName = docName, pOutputFile = null, pDataType = "RAW" };
                    if (StartDocPrinter(hPrinter, 1, docInfo))
                    {
                        if (StartPagePrinter(hPrinter))
                        {
                            bool success = WritePrinter(hPrinter, pBytes, count, out _);
                            EndPagePrinter(hPrinter);
                            return success;
                        }
                        EndDocPrinter(hPrinter);
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    ClosePrinter(hPrinter);
                }
            }
            return false;
        }

        public static bool SendStringToPrinter(string printerName, string content, string docName = "")
        {
            docName = string.IsNullOrEmpty(docName) ? "Documento" : docName;

            IntPtr pBytes = Marshal.StringToCoTaskMemAnsi(content);
            try
            {
                return SendBytesToPrinter(printerName, pBytes, content.Length, docName);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pBytes);
            }
        }

        public static bool SendArrayListToPrinter(string printerName, ArrayList content)
        {
            foreach (string line in content)
            {
                SendStringToPrinter(printerName, line + "\n");
            }
            return true;
        }

        public static bool SendEnumerableToPrinter(string printerName, IEnumerable<(int yoffset, int xoffset, string cadena)> content)
        {
            var alPrint = new ArrayList();
            int currentYOffset = 0;
            foreach (var (yoffset, xoffset, cadena) in content)
            {
                if (yoffset != currentYOffset)
                {
                    while (currentYOffset++ < yoffset)
                    {
                        alPrint.Add(" ");
                    }
                    currentYOffset = yoffset;
                }

                string line = cadena.PadLeft(xoffset);
                alPrint.Add(line);
            }

            return SendArrayListToPrinter(printerName, alPrint);
        }
    }
}

