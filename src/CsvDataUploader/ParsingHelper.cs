using System.Text.RegularExpressions;

namespace CsvDataUploader;

internal static class ParsingHelper
{
    /// <summary>
    /// Custom method to parse a string value to a numeric value. Checks for mixed type survey responses e.g. 10 - Extremely Satisfied or 0 - Extremely Dissatisfied
    /// </summary>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    internal static bool TryGetNumericValue(string value, out decimal result)
    {
        // Regex pattern looking for a number followed by a space and a hyphen
        // will be used to identify numeric answers e.g. 10 - Extremely Satisfied
        // also includes a check for a negative number
        // we don't want to grab all numbers as some text answers may contain numbers in them such as account
        // numbers e.g. 
        var regex = new Regex(@"^-?\d{1,2}(?= -)");

        Match match = regex.Match(value);

        var parseForResult = match.Success ? match.Value : value;

        // is there any benefit to trying int parse before decimal parse here?
        decimal? numericVal = int.TryParse(parseForResult, out var intLiteral)
            ? Convert.ToDecimal(intLiteral)
            : decimal.TryParse(parseForResult, out var decimalLiteral) ? decimalLiteral : null;

        result = numericVal ?? 0;

        return numericVal.HasValue;
    }
}
