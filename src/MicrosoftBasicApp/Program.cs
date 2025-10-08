using MicrosoftBasicApp.Runtime;

if (EmbeddedProgramRunner.TryRunEmbeddedProgram())
{
    return;
}

var interpreter = new BasicInterpreter(new ConsoleBasicIO());
interpreter.RunInteractive();
