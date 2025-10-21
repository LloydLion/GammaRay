using GammaRay.Core.Routing;
using Microsoft.Extensions.Options;

namespace GammaRay.Core.Persistence;

public class RoutePersistenceStorage(
	IOptions<RoutePersistenceStorage.Options> options
) : IRoutePersistenceStorage
{
	private readonly Options _options = options.Value;
	private readonly SemaphoreSlim _semaphore = new(1);
	private Dictionary<(string Site, string Profile), string>? _data;


	public Dictionary<(string Site, string Profile), string> Data => _data ?? throw new InvalidOperationException("Preload database first");


	public void SaveRoute(Site site, NetworkProfile profile, string optimalConfigurationName)
	{
		Data[(site.DomainName, profile.Name)] = optimalConfigurationName;
		SaveDatabase();
	}

	public string? TryGetRoute(Site site, NetworkProfile profile)
	{
		return Data.GetValueOrDefault((site.DomainName, profile.Name));
	}

	public void PreloadDatabase()
	{
		if (File.Exists(_options.DatabasePath) == false)
		{
			_data = [];
			return;
		}

		_data = File.ReadAllLines(_options.DatabasePath)
			.Select(s => s.Split('|'))
			.Where(s => s.Length == 3)
			.ToDictionary(s => (s[0], s[1]), s => s[2]);
	}

	private async void SaveDatabase()
	{
		if (_semaphore.CurrentCount == 0)
			return;
		try
		{
			await _semaphore.WaitAsync();

			var dataCopy = Data.ToDictionary();
			await File.WriteAllLinesAsync(_options.DatabasePath, dataCopy.Select(s => $"{s.Key.Site}|{s.Key.Profile}|{s.Value}"));
		}
		catch (Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Blue;
			Console.WriteLine("FAILED TO SAVE DATABASE: " + ex.ToString());
			Console.ResetColor();
		}
		finally
		{
			_semaphore.Release();
		}
	}


	public class Options
	{
		public string DatabasePath { get; set; } = "routes.dat";
	}
}
