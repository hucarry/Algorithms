using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HBox;
public class FolderProcessor
{
    /// <summary>
    /// 文件搜索配置选项
    /// </summary>
    public class SearchOptions
    {
        public int MaxSearchDepth { get; set; } = -1; // -1 表示不限制深度，0 表示只在当前目录
        public string Filter { get; set; } = "*.*";  // 过滤条件
        public bool UseRegex { get; set; } = false; // 是否使用正则表达式
        public bool IgnoreCase { get; set; } = true; // 是否忽略大小写
        public bool IncludeHidden { get; set; } = false; // 是否包含隐藏文件
        public SearchOption SearchOption => MaxSearchDepth == -1
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly; // 针对原始 Directory.GetFiles
    }

    /// <summary>
    /// 获取匹配条件的文件路径列表（同步）
    /// </summary>
    public static List<string> GetFilePaths(string rootPath, SearchOptions options)
    {
        var files = new List<string>();
        foreach (var file in EnumerateFiles(rootPath, options))
        {
            files.Add(file);
        }
        return files;
    }

    /// <summary>
    /// 获取匹配条件的文件信息列表（返回 FileInfo）
    /// </summary>
    public static List<FileInfo> GetFileInfos(string rootPath, SearchOptions options)
    {
        var fileInfos = new List<FileInfo>();
        foreach (var file in EnumerateFiles(rootPath, options))
        {
            try
            {
                fileInfos.Add(new FileInfo(file));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无法创建 FileInfo 对象: {file}, 错误: {ex.Message}");
            }
        }
        return fileInfos;
    }

    /// <summary>
    /// 异步获取文件路径列表
    /// </summary>
    public static async Task<List<string>> GetFilePathsAsync(string rootPath, SearchOptions options)
    {
        List<string> result = new List<string>();
        await Task.Run(() =>
        {
            foreach (var file in EnumerateFiles(rootPath, options))
            {
                result.Add(file);
            }
        });
        return result;
    }

    /// <summary>
    /// 延迟加载遍历文件（效率更高）
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string rootPath, SearchOptions options)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"无法找到路径: {rootPath}");

        if (options.MaxSearchDepth == -1)
        {
            // 使用 .NET 原生递归，效率更高
            foreach (var file in Directory.EnumerateFiles(rootPath, options.Filter, SearchOption.AllDirectories))
            {
                if (!options.IncludeHidden && IsHidden(file))
                    continue;

                yield return file;
            }
        }
        else
        {
            // 自定义递归处理深度限制
            foreach (var file in EnumerateWithDepthLimitation(rootPath, options.MaxSearchDepth, options))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// 使用自定义深度限制的递归查找
    /// </summary>
    private static IEnumerable<string> EnumerateWithDepthLimitation(string rootPath, int depthLeft, SearchOptions options)
    {
        foreach (var file in EnumerateDirectoryFiles(rootPath, options.Filter, options.UseRegex, options.IgnoreCase, options.IncludeHidden))
        {
            yield return file;
        }

        if (depthLeft > 0)
        {
            foreach (var subDir in Directory.GetDirectories(rootPath))
            {
                if (!options.IncludeHidden && IsHidden(subDir))
                    continue;

                foreach (var file in EnumerateWithDepthLimitation(subDir, depthLeft - 1, options))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoryFiles(string path, string filter, bool useRegex, bool ignoreCase, bool includeHidden)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(path);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"无权访问目录: {path}，错误: {ex.Message}");
            yield break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取目录失败: {path}，错误: {ex.Message}");
            yield break;
        }

        // 将过滤后的文件逐个返回
        foreach (string file in files)
        {
            if (!includeHidden && IsHidden(file))
                continue;

            if (FilterFile(file, filter, useRegex, ignoreCase))
            {
                yield return file;
            }
        }
    }


    /// <summary>
    /// 判断文件名是否匹配过滤条件
    /// </summary>
    private static bool FilterFile(string filePath, string filter, bool useRegex, bool ignoreCase)
    {
        string fileName = Path.GetFileName(filePath);
        if (useRegex)
        {
            var regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            return Regex.IsMatch(fileName, filter, regexOptions);
        }
        else
        {
            string pattern = "^" + Regex.Escape(filter)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            return Regex.IsMatch(fileName, pattern, regexOptions);
        }
    }

    /// <summary>
    /// 检测文件/目录是否隐藏
    /// </summary>
    private static bool IsHidden(string path)
    {
        try
        {
            FileAttributes attr = File.GetAttributes(path);
            return (attr & FileAttributes.Hidden) != 0;
        }
        catch
        {
            return false;
        }
    }
}
