using System.Collections;

namespace GammaRay.Core.Proxy
{
	public class HttpHeadersCollection : IEnumerable<(string Key, string Value)>
	{
		private readonly Dictionary<string, object> _headers = new();


		public int Count { get; private set; }


		public void Add(string header, string value)
		{
			if (_headers.TryGetValue(header, out var obj))
			{
				if (obj is List<string> list)
					list.Add(value);
				else _headers[header] = new List<string>() { (string)obj, value };
			}
			else _headers.Add(header, value);
			Count++;
		}

		public void Add((string, string) pair) => Add(pair.Item1, pair.Item2);

		public string? TryGetSingle(string header)
		{
			if (_headers.TryGetValue(header, out var obj) && obj is string value)
				return value;
			return null;
		}

		public IEnumerable<string> GetAll(string header)
		{
			if (_headers.TryGetValue(header, out var obj))
			{
				if (obj is List<string> list)
					return list;
				else return [(string)obj];
			}
			return Enumerable.Empty<string>();
		}

		public void RemoveAll(string header)
		{
			if (_headers.Remove(header, out var obj))
				Count -= obj is List<string> list ? list.Count : 1;
		}


		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<(string Key, string Value)> GetEnumerator()
		{
			foreach (var kv in _headers)
			{
				if (kv.Value is List<string> list)
				{
					foreach (var value in list)
						yield return (kv.Key, value);
				}
				else yield return (kv.Key, (string)kv.Value);
			}
		}
	}
}
