using System;
using System.Globalization;
using System.IO;
using MicrosoftBasicApp.Runtime;
using Xunit;

namespace MicrosoftBasicApp.Tests;

public class BasicInterpreterTests
{
    [Fact]
    public void RunProgram_PrintsExpectedOutput()
    {
        var program = BuildProgram(
            "10 PRINT \"HELLO\"",
            "20 PRINT 2+2",
            "30 END");

        var io = new BufferedBasicIO();
        Execute(program, io);
        var output = io.GetBuffer();

        Assert.Contains("HELLO", output);
        Assert.Contains("4", output);
    }

    [Fact]
    public void IfGoto_EvaluatesCondition()
    {
        var program = BuildProgram(
            "10 I=0",
            "20 IF I=5 THEN 60",
            "30 I=I+1",
            "40 GOTO 20",
            "50 END",
            "60 PRINT I");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("5", io.GetBuffer());
    }

    [Fact]
    public void ForNext_LoopsAccumulate()
    {
        var program = BuildProgram(
            "10 S=0",
            "20 FOR I=1 TO 5",
            "30 S=S+I",
            "40 NEXT I",
            "50 PRINT S",
            "60 END");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("15", io.GetBuffer());
    }

    [Fact]
    public void Gosub_ReturnsToCaller()
    {
        var program = BuildProgram(
            "10 GOSUB 100",
            "20 PRINT X",
            "30 END",
            "100 X=42",
            "110 RETURN");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("42", io.GetBuffer());
    }

    [Fact]
    public void Arrays_StoreValues()
    {
        var program = BuildProgram(
            "10 DIM A(5)",
            "20 FOR I=0 TO 5",
            "30 A(I)=I*I",
            "40 NEXT I",
            "50 PRINT A(3)",
            "60 END");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("9", io.GetBuffer());
    }

    [Fact]
    public void StringFunctions_OperateCorrectly()
    {
        var program = BuildProgram(
            "10 A$=\"HELLO\"",
            "20 PRINT LEFT$(A$,2);MID$(A$,3,2)",
            "30 END");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("HELL", io.GetBuffer());
    }

    [Fact]
    public void Input_ReadsValues()
    {
        var program = BuildProgram(
            "10 INPUT \"NUMBER\";N",
            "20 PRINT N*2",
            "30 END");

        var io = new BufferedBasicIO(new[] { "5" });
        Execute(program, io);
        Assert.Contains("10", io.GetBuffer());
    }

    private static BasicProgram BuildProgram(params string[] lines)
    {
        var program = new BasicProgram();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex < 0)
            {
                var number = int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture);
                program.SetLine(number, string.Empty);
            }
            else
            {
                var number = int.Parse(trimmed[..spaceIndex], System.Globalization.CultureInfo.InvariantCulture);
                var source = trimmed[(spaceIndex + 1)..];
                program.SetLine(number, source);
            }
        }

        return program;
    }

    private static void Execute(BasicProgram program, BufferedBasicIO io)
    {
        var runtime = new BasicRuntime(program.Compile(), io);
        runtime.ClearVariables();
        runtime.Execute();
    }

    [Fact]
    public void TestBasScript_RunsSuccessfully()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "TEST.BAS");
        var program = BuildProgramFromFile(scriptPath);

        var tempDir = Path.Combine(Path.GetTempPath(), "BasicInterpreterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);

            var io = new BufferedBasicIO(new[] { "5", "A" });
            var runtime = new BasicRuntime(program.Compile(), io);
            runtime.ClearVariables();
            runtime.Execute();

            var output = io.GetBuffer();
            Assert.Contains("READ V(", output, StringComparison.Ordinal);
            Assert.Contains("Unreachable?", output, StringComparison.Ordinal);
            Assert.Contains("Some math functions", output, StringComparison.Ordinal);
            Assert.Contains("Testing IF…THEN…ELSE", output, StringComparison.Ordinal);
            Assert.Contains("Test F(3) =", output, StringComparison.Ordinal);

            var dataPath = Path.Combine(tempDir, "TEST.DAT");
            Assert.True(File.Exists(dataPath));
            var fileContent = File.ReadAllText(dataPath);
            Assert.Contains("Line one", fileContent, StringComparison.Ordinal);
            Assert.Contains("X=", fileContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static BasicProgram BuildProgramFromFile(string path)
    {
        var program = new BasicProgram();
        foreach (var rawLine in File.ReadLines(path))
        {
            var trimmed = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex < 0)
            {
                var number = int.Parse(trimmed, CultureInfo.InvariantCulture);
                program.SetLine(number, string.Empty);
            }
            else
            {
                var number = int.Parse(trimmed[..spaceIndex], CultureInfo.InvariantCulture);
                var source = trimmed[(spaceIndex + 1)..];
                program.SetLine(number, source);
            }
        }

        return program;
    }
}
