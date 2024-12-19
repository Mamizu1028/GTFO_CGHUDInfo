using UnityEngine;

namespace Hikaria.CGHUDInfo.Utils
{
    public class RatioColorDeterminer
	{
		public Color GetDeterminedColor(float ratio, float ratioScale = 1f)
		{
            ratioScale = Mathf.Clamp(ratioScale, 0.001f, 1f);
            Color color = Color.white;
			float num = 0f;
			Color color2 = Color.black;
			for (int i = 0; i < _colorGrades.Count; i++)
			{
				RatioColor ratioColor = _colorGrades[i];
				if (i != 0)
				{
					float num2 = (ratio - num) / (ratioColor.Ratio * ratioScale - num);
					num = ratioColor.Ratio * ratioScale;
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

		public string GetDeterminedColorHTML(float ratio, float ratioScale = 1f)
		{
			return ColorUtility.ToHtmlStringRGBA(GetDeterminedColor(ratio, ratioScale));
		}

		public List<RatioColor> _colorGrades;
	}
}
