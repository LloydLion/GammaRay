namespace GammaRay.Core.Services;

public abstract class CapabilityLinkedValue
{
	public static CapabilityLinkedValue Constant(string value) => new ConstantValue(value);

	public static CapabilityLinkedValue Property(string propertyName) => new PropertyValue(propertyName);

	public abstract string GetValue(Capability capability);


	private class ConstantValue(string _value) : CapabilityLinkedValue
	{
		public override string GetValue(Capability capability) => _value;
	}

	private class PropertyValue(string _propertyName) : CapabilityLinkedValue
	{
		public override string GetValue(Capability capability) => capability.Properties[_propertyName];
	}
}
