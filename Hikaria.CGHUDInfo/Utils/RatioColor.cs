using UnityEngine;

namespace Hikaria.CGHUDInfo.Utils
{
    public class RatioColor
	{
		public RatioColor(Color color, float ratio)
		{
			Color = color;
			Ratio = ratio;
		}

		public Color Color;

		public float Ratio;
	}
}
