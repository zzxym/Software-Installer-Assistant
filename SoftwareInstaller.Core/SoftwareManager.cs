using SoftwareInstaller.Models;
using SoftwareInstaller.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SoftwareInstaller.Core
{
    public class SoftwareManager
    {
        private List<SoftwareItem> _knownSoftwareItems; // 内部知识库
        public List<SoftwareItem> ActiveSoftwareItems { get; private set; } // 当前UI操作的列表
        private const string ConfigPath = "software_list.json";

        private static readonly List<string> SilentArgsList = new List<string>
        {
            "/S", "/VERYSILENT", "/SILENT", "/q", "/qn", "/s", "/quiet"
        };

        public SoftwareManager()
        {
            _knownSoftwareItems = LoadSoftwareItems();
            ActiveSoftwareItems = new List<SoftwareItem>(_knownSoftwareItems); // 启动时，从知识库加载到活动列表
        }

        /// <summary>
        /// 将一个新软件添加到活动列表，并从知识库中为其填充已知信息
        /// 使用安装路径作为唯一索引，禁止重复添加
        /// </summary>
        /// <param name="newSoftware">从UI层传入的新软件对象</param>
        public void AddSoftwareToActiveList(SoftwareItem newSoftware)
        {
            // 使用安装路径作为唯一索引，检查是否已存在
            bool exists = ActiveSoftwareItems.Any(s =>
                !string.IsNullOrEmpty(s.FilePath) &&
                !string.IsNullOrEmpty(newSoftware.FilePath) &&
                s.FilePath.Equals(newSoftware.FilePath, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                // 已存在相同路径的软件，不添加
                return;
            }

            // 尝试在知识库中寻找匹配的软件（这里使用软件名称作为唯一标识）
            var knownItem = _knownSoftwareItems.FirstOrDefault(item => item.Name.Equals(newSoftware.Name, StringComparison.OrdinalIgnoreCase));

            if (knownItem != null)
            {
                // 如果找到了，就用知识库中的信息（特别是静默参数）来更新新软件对象
                newSoftware.SilentInstallArgs = knownItem.SilentInstallArgs;
                newSoftware.Category = knownItem.Category; // 也可以同步分类等其他信息
            }

            // 将处理过的新软件添加到活动列表中
            ActiveSoftwareItems.Add(newSoftware);
        }

        public async Task<(bool success, string? usedArgs)> InstallSoftwareIntelligently(SoftwareItem item, Func<Task<bool>> installCompletedCallback)
        {
            if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
            {
                throw new FileNotFoundException("安装文件未找到。", item.FilePath);
            }

            if (!string.IsNullOrEmpty(item.SilentInstallArgs))
            {
                // 存在静默参数，直接使用，不尝试其他参数
                if (await TryInstall(item.FilePath, item.SilentInstallArgs))
                {
                    await installCompletedCallback();
                    return (true, item.SilentInstallArgs);
                }
                return (false, null);
            }

            foreach (var args in SilentArgsList)
            {
                if (await TryInstall(item.FilePath, args) && await installCompletedCallback())
                {
                    item.SilentInstallArgs = args; // 学习到了新参数
                    SaveSoftwareList(); // 立即保存，确保知识不丢失
                    return (true, args);
                }
            }

            return (false, null);
        }

        private async Task<bool> TryInstall(string filePath, string args)
        {
            try
            {
                using var process = ProcessRunner.StartProcess(filePath, args);
                if (process == null) return false;

                await Task.Delay(2000);

                if (WindowDetector.HasVisibleWindow(process.Id))
                {
                    process.Kill();
                    return false;
                }

                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await process.WaitForExitAsync(cts.Token);

                return process.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 将当前活动列表的更改合并回知识库，并完整保存到JSON文件
        /// </summary>
        public void SaveSoftwareList()
        {
            try
            {
                // 保存所有活动列表中的软件，不仅仅是有静默参数的
                foreach (var activeItem in ActiveSoftwareItems)
                {
                    var knownItem = _knownSoftwareItems.FirstOrDefault(item => item.Name.Equals(activeItem.Name, StringComparison.OrdinalIgnoreCase));
                    if (knownItem != null)
                    {
                        // 如果知识库中已存在，则用最新的信息更新它
                        knownItem.Version = activeItem.Version;
                        knownItem.Description = activeItem.Description;
                        knownItem.Category = activeItem.Category;
                        knownItem.FilePath = activeItem.FilePath; // 更新路径以反映最新选择
                        if (!string.IsNullOrEmpty(activeItem.SilentInstallArgs))
                        {
                            knownItem.SilentInstallArgs = activeItem.SilentInstallArgs; // 只有当有静默参数时才更新
                        }
                    }
                    else
                    {
                        // 如果是全新的软件，则添加到知识库
                        _knownSoftwareItems.Add(activeItem);
                    }
                }

                // 移除知识库中不存在于活动列表的软件
                _knownSoftwareItems = _knownSoftwareItems.Where(knownItem => 
                    ActiveSoftwareItems.Any(activeItem => activeItem.Name.Equals(knownItem.Name, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                // 将完整的知识库序列化并写入文件
                string jsonString = JsonSerializer.Serialize(_knownSoftwareItems, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving software list: {ex.Message}");
            }
        }

        private List<SoftwareItem> LoadSoftwareItems()
        {
            if (!File.Exists(ConfigPath))
            {
                return new List<SoftwareItem>();
            }

            try
            {
                string jsonString = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<List<SoftwareItem>>(jsonString) ?? new List<SoftwareItem>();
            }
            catch (Exception ex)
            {
                throw new Exception($"加载软件列表失败。\n请检查 software_list.json 文件的格式是否正确。", ex);
            }
        }
    }
}
