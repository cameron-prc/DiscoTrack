using System.Globalization;
using System.Text;

namespace DiscoTrack.Discography;

public static class MatchKey
{
	public static string For(string value)
	{
		var key = string.Concat(value
			.Normalize(NormalizationForm.FormD)
			.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
			.Where(char.IsLetterOrDigit))
			.Normalize(NormalizationForm.FormC)
			.ToLowerInvariant();

		return key.Length > 0 ? key : value.Trim().ToLowerInvariant();
	}
}
