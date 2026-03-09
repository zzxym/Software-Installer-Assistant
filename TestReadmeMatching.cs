using System;
using System.IO;
using SoftwareInstaller.Utils;

class TestReadmeMatching
{
    static void Main(string[] args)
    {
        // 测试目录路径
        string testDirectory = "e:\\soft\\输入法";
        
        Console.WriteLine("=== 测试readme.txt文件读取和匹配逻辑 ===");
        Console.WriteLine($"测试目录: {testDirectory}");
        Console.WriteLine();
        
        // 读取readme.txt文件
        Console.WriteLine("1. 读取readme.txt文件...");
        var descriptions = InstallerScanner.ReadReadmeFile(testDirectory);
        
        Console.WriteLine($"读取到{descriptions.Count}个描述:");
        foreach (var kvp in descriptions)
        {
            Console.WriteLine($"- {kvp.Key} -> {kvp.Value}");
        }
        Console.WriteLine();
        
        // 测试文件匹配
        string[] testFiles = {
            "QQPinyin_Setup_6.5.6103.400.exe",
            "QQWubi_Setup_2.2.334.400.exe",
            "sogou_pinyin_9.4.exe"
        };
        
        Console.WriteLine("2. 测试文件匹配...");
        foreach (var fileName in testFiles)
        {
            Console.WriteLine($"\n测试文件: {fileName}");
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string nameWithoutVersion = InstallerScanner.ExtractSoftwareName(fileNameWithoutExt);
            
            Console.WriteLine($"  无扩展名: {fileNameWithoutExt}");
            Console.WriteLine($"  去除版本号: {nameWithoutVersion}");
            
            // 模拟匹配逻辑
            string description = string.Empty;
            bool matched = false;
            
            // 1. 直接匹配
            if (descriptions.TryGetValue(fileName, out var desc1))
            {
                description = desc1;
                Console.WriteLine($"  ✅ 直接匹配成功: {description}");
                matched = true;
            }
            // 2. 去除版本号匹配
            else
            {
                foreach (var kvp in descriptions)
                {
                    string readmeKey = kvp.Key;
                    string readmeNameWithoutVersion = InstallerScanner.ExtractSoftwareName(readmeKey);
                    
                    if (nameWithoutVersion.Equals(readmeNameWithoutVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        description = kvp.Value;
                        Console.WriteLine($"  ✅ 去除版本号匹配成功: {readmeKey} -> {description}");
                        matched = true;
                        break;
                    }
                }
            }
            
            // 3. 关键词匹配
            if (!matched)
            {
                foreach (var kvp in descriptions)
                {
                    string readmeKey = kvp.Key;
                    string readmeValue = kvp.Value;
                    
                    if (nameWithoutVersion.IndexOf(readmeKey, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        readmeKey.IndexOf(nameWithoutVersion, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        description = kvp.Value;
                        Console.WriteLine($"  ✅ 关键词匹配成功: {readmeKey} -> {description}");
                        matched = true;
                        break;
                    }
                    else if (readmeValue.IndexOf(nameWithoutVersion, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        description = kvp.Value;
                        Console.WriteLine($"  ✅ 描述匹配成功: {readmeValue} -> {description}");
                        matched = true;
                        break;
                    }
                }
            }
            
            // 4. 直接匹配readme键
            if (!matched)
            {
                foreach (var kvp in descriptions)
                {
                    string readmeKey = kvp.Key;
                    if (nameWithoutVersion.Equals(readmeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        description = kvp.Value;
                        Console.WriteLine($"  ✅ 直接键匹配成功: {readmeKey} -> {description}");
                        matched = true;
                        break;
                    }
                }
            }
            
            if (!matched)
            {
                Console.WriteLine("  ❌ 未找到匹配的描述");
            }
        }
        
        Console.WriteLine();
        Console.WriteLine("=== 测试完成 ===");
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
}