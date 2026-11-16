using GPMH.Infrastructure.Printing;
using Xunit;

namespace GPMH.UnitTests;

public sealed class MarathiAmountInWordsTests
{
    [Theory]
    [InlineData(0, "शून्य रुपये फक्त")]
    [InlineData(1, "एक रुपये फक्त")]
    [InlineData(46, "शेचाळीस रुपये फक्त")]
    [InlineData(100, "शंभर रुपये फक्त")]
    [InlineData(146, "एकशे शेचाळीस रुपये फक्त")]
    [InlineData(246, "दोनशे शेचाळीस रुपये फक्त")]
    [InlineData(1000, "एक हजार रुपये फक्त")]
    [InlineData(1100, "एक हजार शंभर रुपये फक्त")]
    [InlineData(1844, "एक हजार आठशे चव्वेचाळीस रुपये फक्त")]
    [InlineData(12500, "बारा हजार पाचशे रुपये फक्त")]
    [InlineData(100000, "एक लाख रुपये फक्त")]
    [InlineData(1234567, "बारा लाख चौतीस हजार पाचशे सदुसष्ठ रुपये फक्त")]
    [InlineData(10500205, "एक कोटी पाच लाख दोनशे पाच रुपये फक्त")]
    public void ToRupeesWords_ConvertsWholeAmountsAccurately(decimal amount, string expectedWords)
    {
        string result = MarathiNumberToWords.ToRupeesWords(amount);
        Assert.Equal(expectedWords, result);
    }

    [Theory]
    [InlineData(246.50, "दोनशे शेचाळीस रुपये आणि पन्नास पैसे फक्त")]
    [InlineData(0.75, "पंच्याहत्तर पैसे फक्त")]
    [InlineData(1999.996, "दोन हजार रुपये फक्त")]
    public void ToRupeesWords_HandlesPaiseAndRounding(decimal amount, string expectedWords)
    {
        string result = MarathiNumberToWords.ToRupeesWords(amount);
        Assert.Equal(expectedWords, result);
    }
}

