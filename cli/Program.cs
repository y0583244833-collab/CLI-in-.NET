using System.CommandLine;
var rootCommand = new RootCommand("Root command for File Bundler CLI");
var bundleCommand = new Command("bundle", "Bundle code files to a single file");
rootCommand.Subcommands.Add(bundleCommand);
Option<FileInfo> outputOption = new("--output", "-o")
{
    Description = "the name of the bundle path",
    Required = true
};
Option<string[]> languageOption = new("--language", "-l")
{
    Description = "an option to choose the programing languages",
    Required = true,
    AllowMultipleArgumentsPerToken = true,
};
Option<bool> noteOption = new("--note", "-n")
{
    Description = "add source file path as a comment",
};
Option<string> sortOption = new("--sort", "-s")
{
    Description = "sort order :name or 'type'",
    DefaultValueFactory = _ => "name",
};
Option<bool> removeEmptyLinesOption = new("--remove-empty-lines", "-r")
{
    Description = "remove empty lines from the code",
};
Option<string> authorOption = new("--author", "-a")
{
    Description = "write the auther name o top of file",
};
sortOption.AcceptOnlyFromAmong("name", "type");
bundleCommand.Options.Add(languageOption);
bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(sortOption);
bundleCommand.Options.Add(removeEmptyLinesOption);
bundleCommand.Options.Add(authorOption);
var supportedLanguages = new Dictionary<string, string>()
{
    ["csharp"] = ".cs",
    ["java"] = ".java",
    ["python"] = ".py",
    ["javascript"] = ".js",
    ["typescript"] = ".ts",
    ["html"] = ".html",
    ["css"] = ".css"
};
string[] excludeDirectories = { "bin", "obj", "debug", "release", "node_modules", ".git", ".vs" };
bundleCommand.SetAction(ParseResult =>
{
    var languages = ParseResult.GetValue(languageOption)!;
    var output = ParseResult.GetValue(outputOption);
    var note = ParseResult.GetValue(noteOption);
    var sort = ParseResult.GetValue(sortOption);
    var removeEmpty = ParseResult.GetValue(removeEmptyLinesOption);
    var author = ParseResult.GetValue(authorOption);
    bool containsAll = languages.Any(l => l.Equals("all", StringComparison.OrdinalIgnoreCase));
    if (containsAll && languages.Length > 1)
    {
        Console.WriteLine("Error: 'all' cannot be combined with other languages.");
        return;
    }
    var invalid = languages.Where(l => !l.Equals("all", StringComparison.OrdinalIgnoreCase) &&
        !supportedLanguages.ContainsKey(l.ToLowerInvariant())).ToList();
    if (invalid.Count > 0)
    {
        Console.WriteLine($"Error: unsupported language(s): {string.Join(", ", invalid)}");
        return;
    }
    var currentDir = Directory.GetCurrentDirectory();
    List<string> allFilesPath = new List<string>();
    ScanDirectory(currentDir, excludeDirectories, allFilesPath);
    HashSet<string> extensions;
    if (containsAll)
    {
        extensions = supportedLanguages.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    else
    {
        extensions = languages
            .Select(l => supportedLanguages[l.ToLowerInvariant()])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    var filteredFiles = allFilesPath.Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)).ToList();
    filteredFiles = (sort == "type"
      ? filteredFiles.OrderBy(f => Path.GetExtension(f)).ThenBy(f => Path.GetFileName(f))
      : filteredFiles.OrderBy(f => Path.GetFileName(f))).ToList();
    string outputPath;
    try
    {
        outputPath = Path.GetFullPath(output.FullName);
        string? outputDirectory = Path.GetDirectoryName(outputPath);

        if (string.IsNullOrWhiteSpace(outputDirectory) ||
            !Directory.Exists(outputDirectory))
        {
            Console.WriteLine("Error: The output directory does not exist.");
            return;
        }
    }
    catch (Exception ex) when (
        ex is ArgumentException ||ex is NotSupportedException ||ex is PathTooLongException)
    {
        Console.WriteLine($"Error: Invalid output path. {ex.Message}");
        return;
    }
    filteredFiles = filteredFiles.Where(file => !Path.GetFullPath(file).Equals(outputPath, StringComparison.OrdinalIgnoreCase)).ToList();
    string? authorComment = null;
    if (!string.IsNullOrWhiteSpace(author))
    {
        authorComment = $"Author {author}";
    }
    try
    {
        using var writer = new StreamWriter(outputPath);
        if (authorComment != null)
        {
            writer.WriteLine(authorComment);
        }
        foreach (var file in filteredFiles)
        {
            string content = File.ReadAllText(file);
            if (note)
            {
                string relativePath = Path.GetRelativePath(currentDir, file);
                string sourceComment = GetSourceComment(
                    Path.GetExtension(file),
                    relativePath
                );
                writer.WriteLine(sourceComment);
            }
            if (removeEmpty)
            {
                content = string.Join(
                    Environment.NewLine,
                    content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).Where(line => !string.IsNullOrWhiteSpace(line))
                );
            }
            writer.WriteLine(content);
            writer.WriteLine();
        }
        if (filteredFiles.Count == 0)
            Console.WriteLine("Warning: no matching code files were found.");
        else
            Console.WriteLine($"Bundle created: {outputPath} ({filteredFiles.Count} files)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: could not write the file - {ex.Message}");
    }
});
static void ScanDirectory(string directoryPath, string[] excludeDirectories, List<string> allFiles)
{
    var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly);
    var directories = Directory.EnumerateDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly);
    allFiles.AddRange(files);
    foreach (var directory in directories)
    {
        var directoryName = Path.GetFileName(directory);
        if (excludeDirectories.Contains(directoryName, StringComparer.OrdinalIgnoreCase))
        {
            continue;
        }

        ScanDirectory(directory, excludeDirectories, allFiles);
    }
}
var create_rsp = new Command("create-rsp", "create a response file with ready command");
create_rsp.SetAction(_ =>
{
    string languages = "";
    bool note, removeEmpty;
    while (true)
    {
        Console.WriteLine("Enter the programming languages (separated by space):");
        languages = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(languages))
        {
            Console.WriteLine("you must enter at least one lang");
            continue;
        }
        var words = languages.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Any(w => w.Equals("all", StringComparison.OrdinalIgnoreCase)) && words.Length > 1)
        {
            Console.WriteLine(
                "Error: 'all' cannot be combined with other languages."
            );
            continue;
        }
        var invalid = words.Where(w => !w.Equals("all", StringComparison.OrdinalIgnoreCase) && !supportedLanguages.ContainsKey(w.ToLower())).ToList();
        if (invalid.Count > 0)
        {
            Console.WriteLine($"Not valid: {string.Join(", ", invalid)}. Enter again.");
            continue;
        }
        break;
    }

    Console.WriteLine("Output file path:");
    string outputPath = (Console.ReadLine() ?? "").Trim();
    while (true)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine("Output path is required.");
            outputPath = (Console.ReadLine() ?? "").Trim();
        }
        else
            break;
    }
    note = AskBoolian("could you like to include a note ? ");
    removeEmpty = AskBoolian("Remove empty lines? ");
    Console.Write("Sort by 'name' or 'type' (Enter = name): ");
    string sort = (Console.ReadLine() ?? "").Trim().ToLower();
    if (sort != "type")
        sort = "name";
    Console.Write("Author name (Enter to skip): ");
    string author = (Console.ReadLine() ?? "").Trim();
    Console.WriteLine("how do you want to call the response file???");
    string rsp_name = (Console.ReadLine() ?? "").Trim();
    if (string.IsNullOrWhiteSpace(rsp_name))
    {
        rsp_name = "responseFile.rsp";
    }
    else if (!rsp_name.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase))
        rsp_name += ".rsp";
    var tokens = new List<string> { "bundle", "--language" };
    tokens.AddRange(languages.Split(" ", StringSplitOptions.RemoveEmptyEntries));
    tokens.Add("--output");
    tokens.Add($"\"{outputPath}\"");
    tokens.Add("--sort");
    tokens.Add(sort);
    if (author != "")
    {
        tokens.Add("--author");
        tokens.Add($"\"{author}\"");
    }
    if (note)
    {
        tokens.Add("--note");
    }
    if (removeEmpty)
    {
        tokens.Add("--remove-empty-lines");
    }
    try
    {
        using var writer = new StreamWriter(rsp_name);
        writer.WriteLine(string.Join(" ", tokens));
        Console.WriteLine($"Response file created: {Path.GetFullPath(rsp_name)}");
        Console.WriteLine($"Run it with:  cli @{rsp_name}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
});
rootCommand.Subcommands.Add(create_rsp);
return await rootCommand.Parse(args).InvokeAsync();
static bool AskBoolian(string question)
{
    Console.Write($"{question} (y/n): ");
    return (Console.ReadLine() ?? "").Trim().ToLower() is "y" or "yes" or "true";
}
static string GetSourceComment(string extension, string relativePath)
{
    return extension.ToLowerInvariant() switch
    {
        ".py" => $"# Source: {relativePath}",
        ".html" => $"<!-- Source: {relativePath} -->",
        ".css" => $"/* Source: {relativePath} */",
        ".cs" or ".java" or ".js" or ".ts"
            => $"// Source: {relativePath}",
        _ => $"// Source: {relativePath}"
    };
}