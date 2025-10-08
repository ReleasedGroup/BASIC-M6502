using MicrosoftBasicApp.Parsing;

namespace MicrosoftBasicApp.Runtime;

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public sealed class BasicInterpreter
{
    private readonly IBasicIO _io;
    private readonly BasicProgram _program = new();
    private string? _currentProgramPath;

    public BasicInterpreter(IBasicIO io)
    {
        _io = io;
    }

    public void RunInteractive()
    {
        _io.WriteLine("MICROSOFT BASIC FOR DOTNET 10");
        _io.WriteLine("READY.");

        while (true)
        {
            _io.Write("> ");
            var input = _io.ReadLine();
            if (input is null)
            {
                break;
            }

            var trimmed = input.TrimEnd();
            if (!ProcessCommand(trimmed))
            {
                break;
            }
        }
    }

    public bool ProcessCommand(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        try
        {
            if (TryHandleProgramLine(line))
            {
                return true;
            }

            var upper = line.Trim().ToUpperInvariant();
            if (upper == "RUN")
            {
                RunProgram();
                return true;
            }

            if (upper == "LIST")
            {
                ListProgram();
                return true;
            }

            if (upper == "NEW")
            {
                _program.Clear();
                _io.WriteLine("READY.");
                return true;
            }

            if (upper == "CLEAR")
            {
                ExecuteImmediate("CLEAR");
                return true;
            }

            if (TryMatchCommand(line, "LOAD", out var loadArgs))
            {
                HandleLoad(loadArgs);
                return true;
            }

            if (TryMatchCommand(line, "SAVE", out var saveArgs))
            {
                HandleSave(saveArgs);
                return true;
            }

            if (TryMatchCommand(line, "COMPILE", out var compileArgs))
            {
                HandleCompile(compileArgs);
                return true;
            }

            if (upper is "BYE" or "EXIT" or "QUIT")
            {
                return false;
            }

            ExecuteImmediate(line);
            return true;
        }
        catch (BasicException ex)
        {
            _io.WriteLine($"?{ex.Message}");
            return true;
        }
    }

    public string DumpProgram()
    {
        var builder = new System.Text.StringBuilder();
        foreach (var (number, source) in _program.GetLines())
        {
            builder.Append(number);
            if (!string.IsNullOrEmpty(source))
            {
                builder.Append(' ');
                builder.Append(source);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public void LoadProgram(IEnumerable<string> lines)
    {
        _program.Clear();
        foreach (var line in lines)
        {
            ProcessCommand(line);
        }
    }

    private void RunProgram()
    {
        if (_program.IsEmpty)
        {
            _io.WriteLine("READY.");
            return;
        }

        try
        {
            var compiled = _program.Compile();
            var runtime = new BasicRuntime(compiled, _io);
            runtime.ClearVariables();
            runtime.Execute();
        }
        catch (BasicException ex)
        {
            _io.WriteLine($"?{ex.Message}");
        }
        finally
        {
            _io.WriteLine("READY.");
        }
    }

    private void ListProgram()
    {
        foreach (var (number, source) in _program.GetLines())
        {
            _io.Write(number.ToString());
            if (!string.IsNullOrEmpty(source))
            {
                _io.Write(" ");
                _io.Write(source);
            }

            _io.WriteLine();
        }
    }

    private void ExecuteImmediate(string source)
    {
        try
        {
            var parser = new BasicParser();
            var statements = parser.ParseLine(source);
            if (statements.Count == 0)
            {
                return;
            }

            var compiled = new CompiledProgram(new List<CompiledLine>
            {
                new(0, statements)
            });

            var runtime = new BasicRuntime(compiled, _io);
            runtime.ClearVariables();
            runtime.Execute();
        }
        catch (BasicException ex)
        {
            _io.WriteLine($"?{ex.Message}");
        }
    }

    private bool TryHandleProgramLine(string line)
    {
        var span = line.AsSpan().TrimStart();
        var index = 0;
        while (index < span.Length && char.IsDigit(span[index]))
        {
            index++;
        }

        if (index == 0)
        {
            return false;
        }

        var numberText = span[..index];
        if (!int.TryParse(numberText, out var number))
        {
            throw new BasicSyntaxException($"Invalid line number '{numberText.ToString()}'");
        }

        var remainder = span[index..].ToString().TrimStart();
        _program.SetLine(number, remainder);
        return true;
    }

    private static bool TryMatchCommand(string line, string command, out string arguments)
    {
        var span = line.AsSpan().Trim();
        if (span.Length < command.Length || !span.StartsWith(command, StringComparison.OrdinalIgnoreCase))
        {
            arguments = string.Empty;
            return false;
        }

        if (span.Length == command.Length)
        {
            arguments = string.Empty;
            return true;
        }

        var next = span[command.Length];
        if (!char.IsWhiteSpace(next) && next != '"' && next != '\'' && next != ':')
        {
            arguments = string.Empty;
            return false;
        }

        arguments = span[(command.Length)..].ToString().Trim();
        return true;
    }

    private void HandleLoad(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            throw new BasicRuntimeException("LOAD requires a file name");
        }

        var path = ResolveFilePath(arguments);
        try
        {
            var lines = File.ReadLines(path);
            _program.Clear();
            foreach (var raw in lines)
            {
                var trimmed = raw.TrimEnd();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                ProcessCommand(trimmed);
            }

            _currentProgramPath = path;
            _io.WriteLine("READY.");
        }
        catch (IOException ex)
        {
            throw new BasicRuntimeException(ex.Message);
        }
    }

    private void HandleSave(string arguments)
    {
        var path = string.IsNullOrWhiteSpace(arguments)
            ? _currentProgramPath
            : ResolveFilePath(arguments);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new BasicRuntimeException("SAVE requires a file name");
        }

        try
        {
            var contents = DumpProgram();
            File.WriteAllText(path, contents);
            _currentProgramPath = path;
            _io.WriteLine("READY.");
        }
        catch (IOException ex)
        {
            throw new BasicRuntimeException(ex.Message);
        }
    }

    private void HandleCompile(string arguments)
    {
        if (_program.IsEmpty)
        {
            throw new BasicRuntimeException("No program loaded.");
        }

        var outputPath = ResolveOutputPath(arguments);
        var rid = RuntimeInformation.RuntimeIdentifier;
        if (string.IsNullOrWhiteSpace(rid))
        {
            throw new BasicRuntimeException("Unable to determine runtime identifier.");
        }

        var dotnetExecutable = ResolveDotnetExecutable();
        var projectPath = FindProjectFile();
        var tempDirectory = Directory.CreateTempSubdirectory("msbasic-compile");

        try
        {
            var programFile = Path.Combine(tempDirectory.FullName, "program.bas");
            File.WriteAllText(programFile, DumpProgram(), Encoding.UTF8);

            var publishDir = Path.Combine(tempDirectory.FullName, "publish");
            Directory.CreateDirectory(publishDir);

            var outputLog = new StringBuilder();
            var errorLog = new StringBuilder();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = dotnetExecutable,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            var argumentsList = process.StartInfo.ArgumentList;
            argumentsList.Add("publish");
            argumentsList.Add(projectPath);
            argumentsList.Add("-c");
            argumentsList.Add("Release");
            argumentsList.Add("-r");
            argumentsList.Add(rid);
            argumentsList.Add("-o");
            argumentsList.Add(publishDir);
            argumentsList.Add("-p:SelfContained=true");
            argumentsList.Add("-p:PublishSingleFile=true");
            argumentsList.Add("-p:PublishAot=true");
            argumentsList.Add("-p:StripSymbols=true");
            argumentsList.Add($"-p:CompiledProgramPath={programFile}");

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    outputLog.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    errorLog.AppendLine(e.Data);
                }
            };

            if (!process.Start())
            {
                throw new BasicRuntimeException("Failed to start dotnet publish.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var details = errorLog.Length > 0 ? errorLog.ToString() : outputLog.ToString();
                throw new BasicRuntimeException($"Compilation failed.{Environment.NewLine}{details}");
            }

            var publishedExecutable = LocatePublishedExecutable(publishDir);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Copy(publishedExecutable, outputPath, true);

            if (!OperatingSystem.IsWindows())
            {
                TryMakeExecutable(outputPath);
            }

            _io.WriteLine($"OUTPUT: {outputPath}");
            _io.WriteLine("READY.");
        }
        catch (IOException ex)
        {
            throw new BasicRuntimeException(ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory.FullName);
        }
    }

    private string ResolveOutputPath(string arguments)
    {
        var path = string.IsNullOrWhiteSpace(arguments)
            ? GenerateDefaultOutputPath()
            : ResolveFilePath(arguments);

        if (OperatingSystem.IsWindows() && string.IsNullOrEmpty(Path.GetExtension(path)))
        {
            path += ".exe";
        }

        return Path.GetFullPath(path);
    }

    private string GenerateDefaultOutputPath()
    {
        string directory;
        string baseName;

        if (!string.IsNullOrWhiteSpace(_currentProgramPath))
        {
            directory = Path.GetDirectoryName(_currentProgramPath) ?? Environment.CurrentDirectory;
            var fileName = Path.GetFileNameWithoutExtension(_currentProgramPath);
            baseName = string.IsNullOrWhiteSpace(fileName) ? "compiled-basic" : fileName;
        }
        else
        {
            directory = Environment.CurrentDirectory;
            baseName = "compiled-basic";
        }

        var output = Path.Combine(directory, baseName);
        if (OperatingSystem.IsWindows())
        {
            output += ".exe";
        }

        return output;
    }

    private static string LocatePublishedExecutable(string publishDirectory)
    {
        var candidates = Directory.EnumerateFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(IsExecutablePath)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new BasicRuntimeException("Unable to locate published executable.");
        }

        var preferred = candidates.FirstOrDefault(path =>
            string.Equals(Path.GetFileNameWithoutExtension(path), "MicrosoftBasicApp", StringComparison.OrdinalIgnoreCase));

        return preferred ?? candidates.First();
    }

    private static bool IsExecutablePath(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return string.Equals(Path.GetExtension(name), ".exe", StringComparison.OrdinalIgnoreCase);
        }

        return !name.Contains('.');
    }

    private static void TryMakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch (Exception)
        {
            // Best effort on platforms that support chmod semantics.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ResolveDotnetExecutable()
    {
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath) && File.Exists(hostPath))
        {
            return hostPath;
        }

        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            var candidate = Path.Combine(dotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "dotnet";
    }

    private static string FindProjectFile()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "MicrosoftBasicApp", "MicrosoftBasicApp.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new BasicRuntimeException("Unable to locate project file for compilation.");
    }

    private static string ResolveFilePath(string arguments)
    {
        var trimmed = arguments.Trim();
        if (trimmed.Length == 0)
        {
            throw new BasicRuntimeException("Expected file name");
        }

        string path;
        if (trimmed[0] == '"' || trimmed[0] == '\'')
        {
            var quote = trimmed[0];
            var end = FindMatchingQuote(trimmed, quote);
            if (end < 0)
            {
                throw new BasicRuntimeException("Unterminated file name");
            }

            var inner = trimmed.Substring(1, end - 1).Replace("\"\"", "\"");
            path = inner;
            trimmed = trimmed[(end + 1)..].TrimStart();
        }
        else
        {
            var terminator = trimmed.IndexOfAny(new[] { ' ', ',', ':' });
            if (terminator >= 0)
            {
                path = trimmed[..terminator];
                trimmed = trimmed[terminator..].TrimStart();
            }
            else
            {
                path = trimmed;
                trimmed = string.Empty;
            }
        }

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            if (trimmed[0] == ',')
            {
                trimmed = trimmed[1..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                throw new BasicRuntimeException("Unexpected text after file name");
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new BasicRuntimeException("Expected file name");
        }

        return Path.GetFullPath(path);
    }

    private static int FindMatchingQuote(string text, char quote)
    {
        var index = 1;
        while (index < text.Length)
        {
            if (text[index] == quote)
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}
