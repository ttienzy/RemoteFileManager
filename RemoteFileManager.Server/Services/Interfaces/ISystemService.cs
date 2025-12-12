using RemoteFileManager.Shared.Models.System;
using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Server.Services.Interfaces
{
    public interface ISystemService
    {
        // Lấy danh sách tiến trình đang chạy
        List<ProcessDto> GetProcesses();

        // Diệt tiến trình theo ID
        void KillProcess(int processId);
    }
}
