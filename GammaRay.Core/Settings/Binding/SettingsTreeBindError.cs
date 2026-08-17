using GammaRay.Core.Settings.Tree;

namespace GammaRay.Core.Settings.Binding;

public abstract class SettingsTreeBindError
{
	public abstract TResult Accept<TResult>(IVisitor<TResult> visitor);


	public static SettingsTreeBindError Or(SettingsTreeBindError[] childErrors) => childErrors.Length switch
	{
		0 => throw new ArgumentException($"{nameof(childErrors)} must not be empty", nameof(childErrors)),
		1 => childErrors[0],
		_ => new OrGroup(childErrors)
	};
	
	public static SettingsTreeBindError Or(IEnumerable<SettingsTreeBindError> childErrors) => Or(childErrors.ToArray());
	
	public static SettingsTreeBindError And(SettingsTreeBindError[] childErrors) => childErrors.Length switch
	{
		0 => throw new ArgumentException($"{nameof(childErrors)} must not be empty", nameof(childErrors)),
		1 => childErrors[0],
		_ => new AndGroup(childErrors)
	};
	
	public static SettingsTreeBindError And(IEnumerable<SettingsTreeBindError> childErrors) => And(childErrors.ToArray());
	
	public static SingleError Single(string message, SettingsTreeNode node) => new SingleError(message, node);
	
	
	public class OrGroup(SettingsTreeBindError[] childErrors) : SettingsTreeBindError
	{
		public SettingsTreeBindError[] ChildErrors { get; } = childErrors;
		
		
		public override TResult Accept<TResult>(IVisitor<TResult> visitor) => visitor.Visit(this);
	}

	public class AndGroup(SettingsTreeBindError[] childErrors) : SettingsTreeBindError
	{
		public SettingsTreeBindError[] ChildErrors { get; } = childErrors;
		
		
		public override TResult Accept<TResult>(IVisitor<TResult> visitor) => visitor.Visit(this);
	}

	public class SingleError(string message, SettingsTreeNode node) : SettingsTreeBindError
	{
		public string Message { get; } = message;
		
		public SettingsTreeNode Node { get; } = node;
		
		
		public override TResult Accept<TResult>(IVisitor<TResult> visitor) => visitor.Visit(this);
	}
	
	
	public interface IVisitor<out TResult>
	{
		public TResult Visit(OrGroup orGroup);
		
		public TResult Visit(AndGroup andGroup);
		
		public TResult Visit(SingleError singleError);
	}
}
