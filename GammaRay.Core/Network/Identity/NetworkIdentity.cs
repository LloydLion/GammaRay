namespace GammaRay.Core.Network.Identity;

public readonly struct NetworkIdentity : IEquatable<NetworkIdentity>
{
	public const char BannedDelimiterChar = '+';


	public NetworkIdentity(string[] identityStrings)
	{
		foreach (var str in identityStrings)
			if (str.Contains(BannedDelimiterChar))
				throw new ArgumentException($"Identity strings must not contain the character '{BannedDelimiterChar}'");
		SerializedForm = string.Join(BannedDelimiterChar, identityStrings);
	}

	public NetworkIdentity(string serializedForm)
	{
		SerializedForm = serializedForm;
	}


	public string SerializedForm { get; }



	public string[] GetIdentityStrings() => SerializedForm.Split(BannedDelimiterChar);

	public override int GetHashCode() => SerializedForm.GetHashCode();

	public bool Equals(NetworkIdentity other) => other.SerializedForm == SerializedForm;

	public override bool Equals(object? obj) => obj is NetworkIdentity other && Equals(other); 

	public override string ToString() => SerializedForm;

	public static bool operator ==(NetworkIdentity left, NetworkIdentity right) => left.Equals(right);

	public static bool operator !=(NetworkIdentity left, NetworkIdentity right) => !(left == right);
}
