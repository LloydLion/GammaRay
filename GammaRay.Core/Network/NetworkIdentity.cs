namespace GammaRay.Core.Network;

public readonly struct NetworkIdentity
{
	public const char BannedDelimiterChar = '+';


	public NetworkIdentity(string[] identityStrings)
	{
		foreach (var str in identityStrings)
		{
			if (str.Contains(BannedDelimiterChar))
				throw new ArgumentException($"Identity strings must not contain the character '{BannedDelimiterChar}'");
		}
		IdentityStrings = identityStrings;
	}

	public NetworkIdentity(string serializedForm)
	{
		IdentityStrings = serializedForm.Split(BannedDelimiterChar);
	}


	public string[] IdentityStrings { get; }


	public string SerializeToString()
	{
		return string.Join(BannedDelimiterChar, IdentityStrings);
	}

	public override int GetHashCode() => IdentityStrings[0].GetHashCode();

	public override bool Equals(object? obj) =>
		obj is NetworkIdentity other && other.IdentityStrings.SequenceEqual(IdentityStrings);
}
