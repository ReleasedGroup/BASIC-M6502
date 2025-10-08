using System.IO;
using System.Reflection;
using System.Text;

namespace MicrosoftBasicApp.Runtime;

internal static class EmbeddedProgramRunner
{
    private const string EmbeddedProgramResourceName = "CompiledProgram.bas";

    public static bool TryRunEmbeddedProgram()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => string.Equals(name, EmbeddedProgramResourceName, StringComparison.Ordinal));

        if (resourceName is null)
        {
            return false;
        }

        BasicProgram program;
        try
        {
            program = LoadProgram(assembly, resourceName);
        }
        catch (BasicException ex)
        {
            Console.Error.WriteLine($"?{ex.Message}");
            return true;
        }

        try
        {
            var io = new ConsoleBasicIO();
            var runtime = new BasicRuntime(program.Compile(), io);
            runtime.ClearVariables();
            runtime.Execute();
        }
        catch (BasicException ex)
        {
            Console.Error.WriteLine($"?{ex.Message}");
        }

        return true;
    }

    private static BasicProgram LoadProgram(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new BasicRuntimeException("Compiled program resource missing.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var programText = reader.ReadToEnd();
        var program = new BasicProgram();

        using var textReader = new StringReader(programText);
        string? raw;
        while ((raw = textReader.ReadLine()) is not null)
        {
            var trimmed = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            AddProgramLine(program, trimmed);
        }

        if (program.IsEmpty)
        {
            throw new BasicRuntimeException("Compiled program is empty.");
        }
        return program;
    }

    private static void AddProgramLine(BasicProgram program, string line)
    {
        var span = line.AsSpan().TrimStart();
        var index = 0;
        while (index < span.Length && char.IsDigit(span[index]))
        {
            index++;
        }

        if (index == 0)
        {
            throw new BasicSyntaxException($"Invalid program line '{line}'");
        }

        var numberSpan = span[..index];
        if (!int.TryParse(numberSpan, out var number))
        {
            throw new BasicSyntaxException($"Invalid line number '{numberSpan.ToString()}'");
        }

        var remainder = span[index..].ToString().TrimStart();
        program.SetLine(number, remainder);
    }
}
