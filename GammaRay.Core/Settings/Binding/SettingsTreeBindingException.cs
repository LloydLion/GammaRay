using System.Text;
using GammaRay.Core.Settings.Tree;

namespace GammaRay.Core.Settings.Binding;

public class SettingsTreeBindingException(SettingsTreeBindError error, SettingsTree settingsTree)
	: Exception(BuildMessage(error, settingsTree))
{
	private static string BuildMessage(SettingsTreeBindError bindError, SettingsTree settingsTree)
	{
		return "Failed to parse settings tree to model\n" + 
			string.Join("\n", bindError.Accept(new MessageVisitor(settingsTree))) + "\n\n";
	}


	public class MessageVisitor(SettingsTree settingsTree) : SettingsTreeBindError.IVisitor<string[]>
	{
		public string[] Visit(SettingsTreeBindError.OrGroup orGroup)
		{
			return orGroup.ChildErrors.SelectMany((childError, index) =>
			{
				var lines = childError.Accept(this).ToArray();
				lines[0] = $"{index}) " + lines[0];
				for (int i = 1; i < lines.Length; i++)
					lines[i] = "   " + lines[i];
				return lines;
			}).Prepend("OR>").ToArray();
		}

		public string[] Visit(SettingsTreeBindError.AndGroup andGroup)
		{
			return andGroup.ChildErrors.SelectMany(childError =>
			{
				var lines = childError.Accept(this);
				lines[0] = "> " + lines[0];
				for (int i = 1; i < lines.Length; i++)
					lines[i] = "  " + lines[i];
				return lines;
			}).ToArray();
		}

		public string[] Visit(SettingsTreeBindError.SingleError singleError)
		{
			return [$"{GetPath(singleError.Node)}: {singleError.Message}"];
		}
		
		private string GetPath(SettingsTreeNode node) => settingsTree.InTreeProperties[node].Path;
	}
}
