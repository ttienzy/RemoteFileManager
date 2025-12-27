namespace RemoteFileManager.Shared.Models.RemoteDesktop
{
    // Loại gói tin
    public enum RdpPacketType : byte
    {
        FrameUpdate = 1,
        CursorUpdate = 2,
        ScreenInfo = 3, // Gửi kích thước màn hình lần đầu
        ClientInput = 4
    }

    // Dữ liệu cho một vùng hình ảnh thay đổi
    public struct RectData
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public byte[] Data; // Dữ liệu pixel đã nén LZ4
    }

    // Dữ liệu con trỏ chuột
    public struct CursorData
    {
        public bool IsVisible;
        public int X;
        public int Y;
        public byte[]? ShapeData; // Null nếu hình dạng không đổi
        public int Type; // I-Beam, Arrow, Hand...
    }
    public enum InputType : byte
    {
        MouseMove = 1,
        MouseDown = 2,
        MouseUp = 3,
        KeyDown = 4,
        KeyUp = 5
    }
}