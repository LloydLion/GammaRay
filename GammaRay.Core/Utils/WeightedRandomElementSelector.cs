namespace GammaRay.Core.Utils;

public class WeightedRandomElementSelector<TElement>
{
	private int _weightsSum;
	private WeightedElement[] _elements;
	
	
	public WeightedRandomElementSelector(IEnumerable<TElement> elements, Func<TElement, int> weightSelector)
	{
		var elementsWithWeights = elements.Select(s => (Element: s, Weight: weightSelector(s))).ToArray();
		
		_elements = new WeightedElement[elementsWithWeights.Length];
		// Sum of weight of all elements before current (inclusive) element in loop, after loop will be equal to sum of weights
		var cumulativeWeight = 0; 
		for (int i = 0; i < elementsWithWeights.Length; i++)
		{
			var current = elementsWithWeights[i];
			cumulativeWeight += current.Weight;
			
			_elements[i] = new WeightedElement(current.Element, cumulativeWeight);
		}
		_weightsSum = cumulativeWeight;
	}


	public TElement Next(Random random)
	{
		var value = random.Next(_weightsSum);

		var left = 0;
		var right = _elements.Length - 1;

		while (left < right)
		{
			var middle = left + (right - left) / 2;

			if (value < _elements[middle].CumulativeWeight)
				right = middle;
			else
				left = middle + 1;
		}

		return _elements[left].Element;
	}


	private readonly record struct WeightedElement(TElement Element, int CumulativeWeight);
}
