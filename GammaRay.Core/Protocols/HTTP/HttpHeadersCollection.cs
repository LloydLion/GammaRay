using System.Collections;
using YamlDotNet.Core.Tokens;

namespace GammaRay.Core.Protocols.HTTP
{
	public class HttpHeadersCollection : IEnumerable<(string Key, string Value)>
	{
		private readonly Dictionary<string, object> _headers = [];


		public HttpHeadersCollection()
		{

		}

		private HttpHeadersCollection(Dictionary<string, object> headers, int count)
		{
			_headers = headers;
			Count = count;
		}


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

		public void Set(string header, string value)
		{
			Count -= CountHeader(header);
			Count += 1;
			_headers[header] = value;
		}

		public void Remove(string header)
		{
			Count -= CountHeader(header);
			_headers.Remove(header);
		}

		public IEnumerable<string> GetAll(string header)
		{
			if (_headers.TryGetValue(header, out var obj))
			{
				if (obj is List<string> list)
					return list;
				else return [(string)obj];
			}
			return [];
		}

		public int CountHeader(string header)
		{
			if (_headers.TryGetValue(header, out var obj))
				return obj is List<string> list ? list.Count : 1;
			return 0;
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

		public HttpHeadersCollection Clone()
		{
			var headersClone = _headers.ToDictionary(kv => kv.Key, kv => kv.Value switch
			{
				List<string> list => new List<string>(list),
				var a => a
			});
			return new HttpHeadersCollection(headersClone, Count);
		}
	}
}
