using DiscoTrack.Discography;

namespace DiscoTrack.Tests.Discography;

public class MatchKeyTests
{
	[Theory]
	[InlineData("Lateralus", "lateralus")]
	[InlineData("LATERALUS", "lateralus")]
	[InlineData("AC/DC", "acdc")]
	[InlineData("  10,000 Days  ", "10000days")]
	[InlineData("\"V\" Is for Vagina", "visforvagina")]
	[InlineData("Sigur Rós", "sigurros")]
	[InlineData("Ænima", "ænima")]
	[InlineData("光", "光")]
	[InlineData("", "")]
	public void Lower_cases_folds_diacritics_and_drops_everything_but_letters_and_digits(string value, string expected)
	{
		Assert.Equal(expected, MatchKey.For(value));
	}

	[Theory]
	[InlineData(" ÷ ", "÷")]
	[InlineData("+", "+")]
	public void Keeps_the_trimmed_value_when_nothing_is_a_letter_or_digit(string value, string expected)
	{
		Assert.Equal(expected, MatchKey.For(value));
	}
}
