using System;

namespace SoftwareInstaller.Models
{
    public class SoftwareItem
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? FilePath { get; set; } // 用于存储安装程序的路径
        public string? SilentInstallArgs { get; set; } // 用于静默安装参数

        public SoftwareItem Clone()
        {
            return (SoftwareItem)this.MemberwiseClone();
        }
    }
}
