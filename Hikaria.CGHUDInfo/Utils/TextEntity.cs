using UnityEngine;

namespace Hikaria.CGHUDInfo.Utils
{
    public class TextEntity
	{
		public TextEntity(string text)
		{
			m_text = text;
		}

		public void AddColor(Color color)
		{
			string text = ColorUtility.ToHtmlStringRGBA(color);
			m_text = string.Concat(new string[] { "<color=#", text, ">", m_text, "</color>" });
		}

		public void AddSize(float ratio)
		{
			m_text = string.Concat(new string[]
			{
				"<size=",
				(ratio * 100f).ToString(),
				"%>",
				m_text,
				"</size>"
			});
		}

		public static implicit operator string(TextEntity component)
		{
			return component.m_text;
		}

		private string m_text;
	}
}
