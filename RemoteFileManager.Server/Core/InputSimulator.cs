using System.Runtime.InteropServices;

namespace RemoteFileManager.Server.Core
{
    public static class InputSimulator
    {
        // Import thư viện Windows
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        // Các hằng số cờ (Flags)
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // --- HÀM XỬ LÝ CHUỘT ---
        public static void MoveMouse(int x, int y)
        {
            SetCursorPos(x, y);
        }

        public static void MouseEvent(bool isDown, bool isLeft)
        {
            uint flags;
            if (isLeft)
                flags = isDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
            else
                flags = isDown ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;

            mouse_event(flags, 0, 0, 0, 0);
        }

        // --- HÀM XỬ LÝ PHÍM ---
        public static void KeyEvent(byte keyVal, bool isDown)
        {
            if (isDown)
                keybd_event(keyVal, 0, 0, 0); // Nhấn xuống
            else
                keybd_event(keyVal, 0, KEYEVENTF_KEYUP, 0); // Nhả ra
        }
    }
}