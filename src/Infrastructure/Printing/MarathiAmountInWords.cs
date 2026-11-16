namespace GPMH.Infrastructure.Printing;

/// <summary>
/// Deterministic Indian-numbering rupee/paise converter producing statutory Marathi text
/// (e.g., <c>246m -> दोनशे शेचाळीस रुपये फक्त</c>) for Namuna 9-Ka bills and Namuna 10/11 receipts.
/// </summary>
public static class MarathiNumberToWords
{
    private static readonly string[] Units0To99 =
    [
        "शून्य", "एक", "दोन", "तीन", "चार", "पाच", "सहा", "सात", "आठ", "नऊ", "दहा",
        "अकरा", "बारा", "तेरा", "चौदा", "पंधरा", "सोळा", "सतरा", "अठरा", "एकोणीस", "वीस",
        "एकवीस", "बावीस", "तेवीस", "चोवीस", "पंचवीस", "सव्वीस", "सत्तावीस", "अठ्ठावीस", "एकोणतीस", "तीस",
        "एकतीस", "बत्तीस", "तेहतीस", "चौतीस", "पस्तीस", "छत्तीस", "सदतीस", "अडतीस", "एकोणचाळीस", "चाळीस",
        "एक्केचाळीस", "बेचाळीस", "त्रेचाळीस", "चव्वेचाळीस", "पंचेचाळीस", "शेचाळीस", "सत्तेचाळीस", "अठ्ठेचाळीस", "एकोणपन्नास", "पन्नास",
        "एक्कावन्न", "बावन्न", "त्रेपन्न", "चोपन्न", "पंचावन्न", "छप्पन्न", "सत्तावन्न", "अठ्ठावन्न", "एकोणसाठ", "साठ",
        "एकसष्ठ", "बासष्ठ", "त्रेसष्ठ", "चौसष्ठ", "पासष्ठ", "सहासष्ठ", "सदुसष्ठ", "अडुसष्ठ", "एकोणसत्तर", "सत्तर",
        "एक्काहत्तर", "बाहत्तर", "त्र्याहत्तर", "चौऱ्याहत्तर", "पंच्याहत्तर", "शहात्तर", "सत्याहत्तर", "अठ्ठ्याहत्तर", "एकोणऐंशी", "ऐंशी",
        "एक्क्याऐंशी", "ब्याऐंशी", "त्र्याऐंशी", "चौऱ्याऐंशी", "पंच्याऐंशी", "शहाऐंशी", "सत्याऐंशी", "अठ्ठ्याऐंशी", "एकोणनव्वद", "नव्वद",
        "एक्क्याण्णव", "ब्याण्णव", "त्र्याण्णव", "चौऱ्याण्णव", "पंच्याण्णव", "शहाण्णव", "सत्याण्णव", "अठ्ठ्याण्णव", "नव्व्याण्णव",
    ];

    public static string ToRupeesWords(decimal amount)
    {
        if (amount < 0m)
        {
            amount = Math.Abs(amount);
        }

        decimal rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        long rupees = (long)Math.Truncate(rounded);
        int paise = (int)Math.Round((rounded - rupees) * 100m, 0, MidpointRounding.AwayFromZero);

        if (paise >= 100)
        {
            rupees += 1;
            paise -= 100;
        }

        if (rupees == 0 && paise == 0)
        {
            return "शून्य रुपये फक्त";
        }

        if (rupees > 0 && paise == 0)
        {
            return $"{ConvertWholeNumber(rupees)} रुपये फक्त";
        }

        if (rupees == 0 && paise > 0)
        {
            return $"{ConvertWholeNumber(paise)} पैसे फक्त";
        }

        return $"{ConvertWholeNumber(rupees)} रुपये आणि {ConvertWholeNumber(paise)} पैसे फक्त";
    }

    public static string ConvertWholeNumber(long number)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Amount cannot be negative.");
        }

        if (number < 100)
        {
            return Units0To99[number];
        }

        List<string> parts = [];

        long crores = number / 1_00_00_000;
        if (crores > 0)
        {
            parts.Add($"{ConvertWholeNumber(crores)} कोटी");
            number %= 1_00_00_000;
        }

        long lakhs = number / 1_00_000;
        if (lakhs > 0)
        {
            parts.Add($"{Units0To99[lakhs]} लाख");
            number %= 1_00_000;
        }

        long thousands = number / 1_000;
        if (thousands > 0)
        {
            parts.Add($"{Units0To99[thousands]} हजार");
            number %= 1_000;
        }

        long hundreds = number / 100;
        long rem = number % 100;
        if (hundreds > 0)
        {
            if (hundreds == 1 && rem == 0)
            {
                parts.Add("शंभर");
            }
            else
            {
                parts.Add($"{Units0To99[hundreds]}शे");
            }
        }

        if (rem > 0)
        {
            parts.Add(Units0To99[rem]);
        }

        return string.Join(' ', parts);
    }
}

