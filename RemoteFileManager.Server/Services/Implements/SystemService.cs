using RemoteFileManager.Server.Services.Interfaces;
using RemoteFileManager.Shared.Models.System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RemoteFileManager.Server.Services.Implements
{
    public class SystemService : ISystemService
    {
        public List<ProcessDto> GetProcesses()
        {
            var processes = Process.GetProcesses();
            var result = new List<ProcessDto>();

            foreach (var p in processes)
            {
                try
                {
                    result.Add(new ProcessDto
                    {
                        Id = p.Id,
                        ProcessName = p.ProcessName,
                        WindowTitle = p.MainWindowTitle,
                        // WorkingSet64 là lượng RAM đang dùng (bytes)
                        MemoryUsage = p.WorkingSet64
                    });
                }
                catch
                {
                    // Một số process hệ thống (System) sẽ không cho đọc thông tin -> Bỏ qua
                }
            }

            // Sắp xếp theo tên cho dễ nhìn
            return result.OrderBy(x => x.ProcessName).ToList();
        }

        public void KillProcess(int processId)
        {
            var process = Process.GetProcessById(processId);
            process.Kill();
        }
    }
}
