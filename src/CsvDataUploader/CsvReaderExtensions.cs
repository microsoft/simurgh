using CsvHelper;

namespace CsvDataUploader;

internal static class CsvReaderExtensions
{
    internal static Dictionary<string, string> GetHeaderDescriptions(this CsvReader csvHeaderDescriptions)
    {
        // Read the first row of the CSV Header Description file to get the headers
        csvHeaderDescriptions.Read();
        var headers = csvHeaderDescriptions.Parser.Record?.Length > 0 ? csvHeaderDescriptions.Parser.Record : throw new InvalidOperationException("The CSV header descriptions file does not contain any headers.");

        // Read the second row of the CSV file to get the descriptions
        csvHeaderDescriptions.Read();
        var descriptions = csvHeaderDescriptions.Parser.Record?.Length > 0 ? csvHeaderDescriptions.Parser.Record : throw new InvalidOperationException("The CSV header descriptions file does not contain any descriptions.");

        // Create a dictionary to store the header-description pairs
        var headerDescriptions = new Dictionary<string, string>();

        // Populate the dictionary
        for (int i = 0; i < headers.Length; i++)
        {
            headerDescriptions[headers[i]] = descriptions[i];
        }

        return headerDescriptions;
    }
}
