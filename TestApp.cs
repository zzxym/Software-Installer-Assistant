using System;
using SoftwareInstaller.Utils;

class TestApp
{
    static void Main(string[] args)
    {
        // 测试目录路径
        string testDirectory = "e:\\soft\\输入法";
        
        // 调用测试方法
        InstallerScanner.TestReadmeMatching(testDirectory);
        
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
}