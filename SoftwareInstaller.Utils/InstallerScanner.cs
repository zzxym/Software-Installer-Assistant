using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SoftwareInstaller.Utils
{
    public static class InstallerScanner
    {
        private static readonly HashSet<string> InstallerExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".msp", ".msu"
        };

        private static readonly string[] ExcludedNames = new[]
        {
            "uninstall", "unins", "setup", "install", "update"
        };

        public static List<InstallerInfo> ScanDirectory(string directoryPath)
        {
            var installers = new List<InstallerInfo>();

            if (!Directory.Exists(directoryPath))
                return installers;

            try
            {
                // 读取readme.txt文件
                var readmeDescriptions = ReadReadmeFile(directoryPath);

                var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var extension = Path.GetExtension(file);

                    if (!InstallerExtensions.Contains(extension))
                        continue;

                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);

                    if (IsLikelyInstaller(fileNameWithoutExt))
                    {
                        // 尝试多种方式匹配文件名
                        string description = string.Empty;
                        System.Diagnostics.Debug.WriteLine($"开始匹配文件: {fileName}");
                        System.Diagnostics.Debug.WriteLine($"文件名（无扩展名）: {fileNameWithoutExt}");
                        
                        // 1. 直接匹配完整文件名
                        if (readmeDescriptions.TryGetValue(fileName, out var desc1))
                        {
                            description = desc1;
                            System.Diagnostics.Debug.WriteLine($"直接匹配成功: {fileName} -> {description}");
                        }
                        // 2. 匹配文件名（不区分大小写）
                        else if (readmeDescriptions.TryGetValue(fileName.ToLower(), out var desc2))
                        {
                            description = desc2;
                            System.Diagnostics.Debug.WriteLine($"不区分大小写匹配成功: {fileName} -> {description}");
                        }
                        // 3. 匹配文件名（去除版本号）
                        else
                        {
                            string nameWithoutVersion = ExtractSoftwareName(fileNameWithoutExt);
                            System.Diagnostics.Debug.WriteLine($"去除版本号后的名称: {nameWithoutVersion}");
                            bool matched = false;
                            
                            // 尝试去除版本号匹配
                            foreach (var kvp in readmeDescriptions)
                            {
                                string readmeFileName = Path.GetFileNameWithoutExtension(kvp.Key);
                                string readmeNameWithoutVersion = ExtractSoftwareName(readmeFileName);
                                System.Diagnostics.Debug.WriteLine($"  尝试匹配: {readmeFileName} -> {readmeNameWithoutVersion}");
                                
                                if (nameWithoutVersion.Equals(readmeNameWithoutVersion, StringComparison.OrdinalIgnoreCase))
                                {
                                    description = kvp.Value;
                                    System.Diagnostics.Debug.WriteLine($"去除版本号匹配成功: {fileName} -> {readmeFileName} -> {description}");
                                    matched = true;
                                    break;
                                }
                            }
                            
                            // 4. 尝试关键词匹配（如果前面的匹配都失败）
                            if (!matched)
                            {
                                System.Diagnostics.Debug.WriteLine("尝试关键词匹配...");
                                foreach (var kvp in readmeDescriptions)
                                {
                                    string readmeKey = kvp.Key.Trim();
                                    string readmeValue = kvp.Value.Trim();
                                    System.Diagnostics.Debug.WriteLine($"  尝试关键词: {readmeKey} -> {readmeValue}");
                                    
                                    // 检查readme中的键是否是软件名称的一部分
                                    if (nameWithoutVersion.IndexOf(readmeKey, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        readmeKey.IndexOf(nameWithoutVersion, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        description = kvp.Value;
                                        System.Diagnostics.Debug.WriteLine($"关键词匹配成功: {fileName} -> {readmeKey} -> {description}");
                                        matched = true;
                                        break;
                                    }
                                    // 检查readme中的值是否包含软件名称
                                    else if (readmeValue.IndexOf(nameWithoutVersion, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        description = kvp.Value;
                                        System.Diagnostics.Debug.WriteLine($"描述匹配成功: {fileName} -> {readmeValue} -> {description}");
                                        matched = true;
                                        break;
                                    }
                                }
                            }
                            
                            // 5. 尝试直接匹配readme中的键（如果前面的匹配都失败）
                            if (!matched)
                            {
                                System.Diagnostics.Debug.WriteLine("尝试直接匹配readme键...");
                                foreach (var kvp in readmeDescriptions)
                                {
                                    string readmeKey = kvp.Key.Trim();
                                    if (nameWithoutVersion.Equals(readmeKey, StringComparison.OrdinalIgnoreCase))
                                    {
                                        description = kvp.Value;
                                        System.Diagnostics.Debug.WriteLine($"直接键匹配成功: {fileName} -> {readmeKey} -> {description}");
                                        matched = true;
                                        break;
                                    }
                                }
                            }
                            
                            if (!matched)
                            {
                                System.Diagnostics.Debug.WriteLine($"未找到匹配的描述: {fileName}");
                            }
                        }
                        
                        var info = new InstallerInfo
                        {
                            FilePath = file,
                            FileName = fileName,
                            Name = ExtractSoftwareName(fileNameWithoutExt),
                            Size = FormatFileSize(new FileInfo(file).Length),
                            Description = description
                        };
                        installers.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描目录失败: {ex.Message}");
            }

            return installers;
        }

        private static Dictionary<string, string> ReadReadmeFile(string directoryPath)
        {
            var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var readmePath = Path.Combine(directoryPath, "readme.txt");

            if (!File.Exists(readmePath))
            {
                System.Diagnostics.Debug.WriteLine($"未找到readme.txt文件: {readmePath}");
                return descriptions;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"开始读取readme.txt文件: {readmePath}");
                
                // 尝试不同编码读取文件
                string[] encodings = { "utf-8", "gb2312", "gbk", "utf-16" };
                string content = string.Empty;
                string usedEncoding = "";

                foreach (var encodingName in encodings)
                {
                    try
                    {
                        var encoding = System.Text.Encoding.GetEncoding(encodingName);
                        content = File.ReadAllText(readmePath, encoding);
                        usedEncoding = encodingName;
                        System.Diagnostics.Debug.WriteLine($"成功使用{encodingName}编码读取文件");
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"使用{encodingName}编码读取失败: {ex.Message}");
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(content))
                {
                    try
                    {
                        // 最后尝试默认编码
                        content = File.ReadAllText(readmePath);
                        usedEncoding = "default";
                        System.Diagnostics.Debug.WriteLine("成功使用默认编码读取文件");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"使用默认编码读取失败: {ex.Message}");
                        return descriptions;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"文件内容长度: {content.Length} 字符");
                System.Diagnostics.Debug.WriteLine($"文件内容: {content}");
                
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                System.Diagnostics.Debug.WriteLine($"文件行数: {lines.Length}");
                
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    var fileNameLine = lines[i].Trim().Trim('\'', '"'); // 去除可能的引号
                    if (string.IsNullOrEmpty(fileNameLine))
                    {
                        System.Diagnostics.Debug.WriteLine($"第{i+1}行为空，跳过");
                        continue;
                    }

                    var descriptionLine = lines[i + 1].Trim();
                    if (string.IsNullOrEmpty(descriptionLine))
                    {
                        System.Diagnostics.Debug.WriteLine($"第{i+2}行为空，跳过");
                        continue;
                    }

                    descriptions[fileNameLine] = descriptionLine;
                    System.Diagnostics.Debug.WriteLine($"添加描述: {fileNameLine} -> {descriptionLine}");
                    
                    // 同时添加一个空格替换为下划线的版本，提高匹配率
                    var fileNameWithUnderscore = fileNameLine.Replace(' ', '_');
                    if (fileNameWithUnderscore != fileNameLine)
                    {
                        descriptions[fileNameWithUnderscore] = descriptionLine;
                        System.Diagnostics.Debug.WriteLine($"添加下划线版本: {fileNameWithUnderscore} -> {descriptionLine}");
                    }
                    // 同时添加一个下划线替换为空格的版本
                    var fileNameWithSpaces = fileNameLine.Replace('_', ' ');
                    if (fileNameWithSpaces != fileNameLine)
                    {
                        descriptions[fileNameWithSpaces] = descriptionLine;
                        System.Diagnostics.Debug.WriteLine($"添加空格版本: {fileNameWithSpaces} -> {descriptionLine}");
                    }
                    i++; // 跳过下一行（描述）
                }
                
                System.Diagnostics.Debug.WriteLine($"成功读取{descriptions.Count}个描述");
                // 打印所有读取到的描述
                foreach (var kvp in descriptions)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {kvp.Key} -> {kvp.Value}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取readme.txt失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
            }

            return descriptions;
        }

        private static bool IsLikelyInstaller(string fileNameWithoutExt)
        {
            var lowerName = fileNameWithoutExt.ToLowerInvariant();

            // 只排除明显的卸载程序，不排除安装程序
            foreach (var excluded in ExcludedNames)
            {
                // 只排除以uninstall或unins开头的文件
                if ((excluded == "uninstall" || excluded == "unins") && lowerName.StartsWith(excluded))
                    return false;
            }

            return true;
        }

        private static string ExtractSoftwareName(string fileNameWithoutExt)
        {
            var name = fileNameWithoutExt;

            var versionPatterns = new[]
            {
                @"\d+\.\d+\.\d+\.\d+",
                @"\d+\.\d+\.\d+",
                @"\d+\.\d+",
                @"v\d+",
                @"_\d+_\d+_\d+_\d+",
                @"_\d+_\d+_\d+",
                @"_\d+_\d+",
                @"-\d+\.\d+",
                @"_x\d+"
            };

            foreach (var pattern in versionPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(name, pattern);
                if (match.Success)
                {
                    name = name.Substring(0, match.Index).TrimEnd('_', '-', ' ', '.');
                    break;
                }
            }

            name = name.Replace('_', ' ').Replace('-', ' ');

            return name;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 搜索并解析soft目录下所有子目录中的readme.txt文件
        /// </summary>
        /// <param name="softDirectoryPath">soft目录路径</param>
        /// <returns>每个子目录的readme.txt解析结果</returns>
        public static Dictionary<string, Dictionary<string, string>> SearchAndParseReadmeFiles(string softDirectoryPath)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            if (!Directory.Exists(softDirectoryPath))
            {
                System.Diagnostics.Debug.WriteLine($"Soft目录不存在: {softDirectoryPath}");
                return result;
            }

            try
            {
                // 获取所有子目录
                var subDirectories = Directory.GetDirectories(softDirectoryPath);
                System.Diagnostics.Debug.WriteLine($"找到{subDirectories.Length}个子目录");

                foreach (var subDir in subDirectories)
                {
                    var subDirName = Path.GetFileName(subDir);
                    var readmePath = Path.Combine(subDir, "readme.txt");

                    if (File.Exists(readmePath))
                    {
                        var descriptions = ReadReadmeFile(subDir);
                        if (descriptions.Count > 0)
                        {
                            result[subDirName] = descriptions;
                            System.Diagnostics.Debug.WriteLine($"子目录 {subDirName} 中找到{descriptions.Count}个软件描述");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"子目录 {subDirName} 中的readme.txt文件为空或格式不正确");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"子目录 {subDirName} 中未找到readme.txt文件");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"搜索readme.txt文件失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 打印readme.txt解析结果
        /// </summary>
        /// <param name="readmeResults">解析结果</param>
        public static void PrintReadmeResults(Dictionary<string, Dictionary<string, string>> readmeResults)
        {
            Console.WriteLine("===== Readme.txt 解析结果 =====");
            Console.WriteLine();

            if (readmeResults.Count == 0)
            {
                Console.WriteLine("未找到任何readme.txt文件或文件内容为空");
                return;
            }

            foreach (var kvp in readmeResults)
            {
                string subDirName = kvp.Key;
                var descriptions = kvp.Value;

                Console.WriteLine($"[子目录: {subDirName}]");
                Console.WriteLine($"找到 {descriptions.Count} 个软件描述:");
                Console.WriteLine("-" + new string('-', 80));

                foreach (var desc in descriptions)
                {
                    Console.WriteLine($"文件名: {desc.Key}");
                    Console.WriteLine($"描述:   {desc.Value}");
                    Console.WriteLine();
                }

                Console.WriteLine();
            }

            Console.WriteLine("==============================");
        }

        // 公开测试方法
        public static Dictionary<string, string> TestReadReadmeFile(string directoryPath)
        {
            return ReadReadmeFile(directoryPath);
        }

        // 公开测试方法
        public static string TestExtractSoftwareName(string fileName)
        {
            return ExtractSoftwareName(fileName);
        }

        // 直接测试方法
        public static void TestReadmeMatching(string testDirectory)
        {
            Console.WriteLine("=== 测试readme.txt文件读取和匹配逻辑 ===");
            Console.WriteLine($"测试目录: {testDirectory}");
            Console.WriteLine();
            
            // 读取readme.txt文件
            Console.WriteLine("1. 读取readme.txt文件...");
            var descriptions = ReadReadmeFile(testDirectory);
            
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
                string nameWithoutVersion = ExtractSoftwareName(fileNameWithoutExt);
                
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
                        string readmeNameWithoutVersion = ExtractSoftwareName(readmeKey);
                        
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
            
            // 测试完整的扫描功能
            Console.WriteLine("\n3. 测试完整的扫描功能...");
            var installers = ScanDirectory(testDirectory);
            Console.WriteLine($"扫描到{installers.Count}个安装程序:");
            foreach (var installer in installers)
            {
                Console.WriteLine($"- {installer.FileName} -> {installer.Description}");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== 测试完成 ===");
        }
    }

    public class InstallerInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
