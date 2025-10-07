using MicrosoftBasicApp.Parsing;

namespace MicrosoftBasicApp.Runtime;

public sealed class BasicInterpreter
{
    private readonly IBasicIO _io;
    private readonly BasicProgram _program = new();

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
}
