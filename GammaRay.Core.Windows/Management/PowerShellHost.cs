using System.Diagnostics;

namespace GammaRay.Core.Windows.Management;

public sealed class PowerShellHost
{
	private const string Prompt = "CMD> ";
	private const string TerminatingCommand = "echo $null";
	private const string Terminator = Prompt + TerminatingCommand;


	private InitializationContext? _init;


	public IEnumerable<string> RunCommand(string commandToRun)
	{
		var context = GetInitializationContext();
		context.PowerShellProcess.StandardInput.WriteLine(commandToRun);
		context.PowerShellProcess.StandardInput.WriteLine(TerminatingCommand);

		context.PowerShellProcess.StandardOutput.ReadLine(); // read command itself

		while (true)
		{
			var newLine = context.PowerShellProcess.StandardOutput.ReadLine();
			if (newLine is null)
				break;
			if (newLine == Terminator)
				break;
			yield return newLine;
		}
	}

	private InitializationContext GetInitializationContext()
	{
		if (_init is null or { PowerShellProcess.HasExited: true })
			_init = InitializePowerShell();
		return _init;
	}

	private static InitializationContext InitializePowerShell()
	{
		var psi = new ProcessStartInfo()
		{
			FileName = "powershell",
			Arguments = "-NoLogo -InputFormat text -OutputFormat text -NoProfile -NonInteractive -Sta",

			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			RedirectStandardInput = true
		};

		var powerShellProcess = Process.Start(psi);
		if (powerShellProcess is null or { HasExited: true })
			throw new Exception("Enable to start power shell process");

		powerShellProcess.StandardInput.WriteLine(
			$$"""
			function prompt { "{{Prompt}}" }
			""");
		powerShellProcess.StandardOutput.ReadLine();

		return new InitializationContext(powerShellProcess);
	}


	private record InitializationContext(Process PowerShellProcess);
}
