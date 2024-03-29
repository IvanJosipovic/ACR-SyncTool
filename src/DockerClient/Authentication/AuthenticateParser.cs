﻿namespace ACR_SyncTool.DockerClient.Authentication;

internal static class AuthenticateParser
{
    public static IDictionary<string, string> Parse(string value)
    {
        return SplitWWWAuthenticateHeader(value).ToDictionary(GetKey, GetValue);
    }

    private static IEnumerable<string> SplitWWWAuthenticateHeader(string value)
    {
        var builder = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < value.Length; i++)
        {
            var charI = value[i];
            switch (charI)
            {
                case '\"':
                    if (inQuotes)
                    {
                        yield return builder.ToString();
                        builder.Clear();
                        inQuotes = false;
                    }
                    else
                    {
                        inQuotes = true;
                    }
                    break;

                case ',':
                    if (inQuotes)
                    {
                        builder.Append(charI);
                    }
                    else
                    {
                        if (builder.Length > 0)
                        {
                            yield return builder.ToString();
                            builder.Clear();
                        }
                    }
                    break;

                default:
                    builder.Append(charI);
                    break;
            }
        }
        if (builder.Length > 0) yield return builder.ToString();
    }

    public static ParsedAuthentication ParseTyped(string value)
    {
        var parsed = Parse(value);

        return new ParsedAuthentication(
#pragma warning disable CS8604 // Possible null reference argument.
            parsed.GetValueOrDefault("realm"),
            parsed.GetValueOrDefault("service"),
            parsed.GetValueOrDefault("scope")
#pragma warning restore CS8604 // Possible null reference argument.
            );
    }

    private static string GetKey(string pair)
    {
        int equalPos = pair.IndexOf("=", StringComparison.Ordinal);

        if (equalPos < 1)
            throw new FormatException("No '=' found.");

        return pair.Substring(0, equalPos);
    }

    private static string GetValue(string pair)
    {
        int equalPos = pair.IndexOf("=", StringComparison.Ordinal);

        if (equalPos < 1)
            throw new FormatException("No '=' found.");

        string value = pair.Substring(equalPos + 1).Trim();

        //Trim quotes
        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Substring(1, value.Length - 2);
        }

        return value;
    }
}