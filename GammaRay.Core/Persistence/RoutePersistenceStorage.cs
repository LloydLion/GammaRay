using GammaRay.Core.Routing;
using System.Collections.Concurrent;

namespace GammaRay.Core.Persistence;

public class RoutePersistenceStorage : IRoutePersistenceStorage
{
	private const string Path = "routes.dat";
	private readonly SemaphoreSlim _semaphore = new(1);
	private ConcurrentDictionary<(string Site, string Profile), string>? _data;

	public ConcurrentDictionary<(string Site, string Profile), string> Data => _data ?? throw new InvalidOperationException("Preload database first");


	public void SaveRoute(Site site, NetworkProfile profile, string optimalConfigurationName)
	{
		Data.AddOrUpdate((site.DomainName, profile.Name), optimalConfigurationName, (key, value) => value);
		SaveDatabase();
	}

	public string? TryGetRoute(Site site, NetworkProfile profile)
	{
		return Data.GetValueOrDefault((site.DomainName, profile.Name));
	}

	public void PreloadDatabase()
	{
		if (File.Exists(Path) == false)
		{
			_data = new();
			return;
		}

		_data = new ConcurrentDictionary<(string Site, string Profile), string>
			(File.ReadAllLines(Path)
			.Select(s => s.Split('|'))
			.Where(s => s.Length == 3)
			.ToDictionary(s => (s[0], s[1]), s => s[2])
		);
	}

	private async void SaveDatabase()
	{
		if (_semaphore.CurrentCount == 0)
			return;
		try
		{
			await _semaphore.WaitAsync();

			var dataCopy = Data.ToDictionary();
			await File.WriteAllLinesAsync(Path, dataCopy.Select(s => $"{s.Key.Site}|{s.Key.Profile}|{s.Value}"));
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
}
