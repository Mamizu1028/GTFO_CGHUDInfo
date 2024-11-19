using UnityEngine;

namespace Hikaria.CGHUDInfo.Utils
{
    public class RatioColorDeterminer
	{
		public Color GetDeterminedColor(float ratio)
		{
			Color color = Color.white;
			float num = 0f;
			Color color2 = Color.black;
			for (int i = 0; i < _colorGrades.Count; i++)
			{
				RatioColor ratioColor = _colorGrades[i];
				if (i != 0)
				{
					float num2 = (ratio - num) / (ratioColor.Ratio - num);
					num = ratioColor.Ratio;
					color = Color.Lerp(color2, ratioColor.Color, Mathf.Clamp01(num2));
					if (num2 < 1f)
					{
						break;
					}
				}
				color2 = ratioColor.Color;
			}
			return color;
		}

		public string GetDeterminedColorHTML(float ratio)
		{
			return ColorUtility.ToHtmlStringRGBA(GetDeterminedColor(ratio));
		}

		public List<RatioColor> _colorGrades;
	}
}
